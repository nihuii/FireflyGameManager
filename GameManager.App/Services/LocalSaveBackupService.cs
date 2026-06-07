using System.IO;
using System.IO.Compression;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class LocalSaveBackupService : ISaveBackupService
{
    private readonly string backupRootDirectory;
    private readonly Func<int> retentionCountProvider;

    public LocalSaveBackupService(string backupRootDirectory, int retentionCount = 20)
        : this(backupRootDirectory, () => retentionCount)
    {
    }

    public LocalSaveBackupService(string backupRootDirectory, Func<int> retentionCountProvider)
    {
        this.backupRootDirectory = backupRootDirectory;
        this.retentionCountProvider = retentionCountProvider;
    }

    public Task<string> BackupAsync(Game game)
    {
        return Task.Run(() =>
        {
            EnsureSaveDirectoryExists(game);
            var backupDirectory = GetBackupDirectory(game);
            Directory.CreateDirectory(backupDirectory);

            var fileName = $"{SafePathSegment.Create(game.Name, "Game")}-{DateTime.Now:yyyyMMdd-HHmmss-fff}.zip";
            var backupPath = Path.Combine(backupDirectory, fileName);
            ZipFile.CreateFromDirectory(game.SavePath, backupPath, CompressionLevel.Optimal, false);
            PruneBackups(game);
            return backupPath;
        });
    }

    public Task RestoreAsync(Game game, string backupPath)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
            {
                throw new FileNotFoundException("存档备份文件不存在。", backupPath);
            }

            if (string.IsNullOrWhiteSpace(game.SavePath))
            {
                throw new InvalidOperationException("当前游戏没有设置存档目录。");
            }

            var restoreSourcePath = backupPath;
            string? temporaryRestorePath = null;
            string? protectionPath = null;
            if (IsInsideDirectory(backupPath, game.SavePath))
            {
                var temporaryDirectory = Path.Combine(Path.GetTempPath(), "FireflyGameManagerRestore");
                Directory.CreateDirectory(temporaryDirectory);
                temporaryRestorePath = Path.Combine(temporaryDirectory, $"{Guid.NewGuid():N}.zip");
                File.Copy(backupPath, temporaryRestorePath);
                restoreSourcePath = temporaryRestorePath;
            }

            try
            {
                if (Directory.Exists(game.SavePath) &&
                    Directory.EnumerateFileSystemEntries(game.SavePath).Any())
                {
                    var backupDirectory = GetBackupDirectory(game);
                    Directory.CreateDirectory(backupDirectory);
                    protectionPath = Path.Combine(
                        backupDirectory,
                        $"{SafePathSegment.Create(game.Name, "Game")}-{DateTime.Now:yyyyMMdd-HHmmss-fff}-before-restore.zip");
                    ZipFile.CreateFromDirectory(game.SavePath, protectionPath, CompressionLevel.Optimal, false);
                }

                Directory.CreateDirectory(game.SavePath);
                ClearDirectory(game.SavePath);
                try
                {
                    ZipFile.ExtractToDirectory(restoreSourcePath, game.SavePath, true);
                }
                catch
                {
                    ClearDirectory(game.SavePath);
                    if (!string.IsNullOrWhiteSpace(protectionPath) && File.Exists(protectionPath))
                    {
                        ZipFile.ExtractToDirectory(protectionPath, game.SavePath, true);
                    }

                    throw;
                }
                PruneBackups(game);
            }
            finally
            {
                TryDeleteFile(temporaryRestorePath);
            }
        });
    }

    public string GetBackupDirectory(Game game)
    {
        var existing = GetBackupDirectories(game).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var gameDirectoryName = $"{SafePathSegment.Create(game.Name, "Game")}-{SafePathSegment.Create(game.Id, "game")}";
        return Path.Combine(backupRootDirectory, gameDirectoryName);
    }

    public IReadOnlyList<SaveBackupEntry> GetBackups(Game game)
    {
        return GetBackupDirectories(game)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.zip", SearchOption.TopDirectoryOnly))
            .Select(path =>
            {
                var file = new FileInfo(path);
                return new SaveBackupEntry(file.FullName, file.Name, file.LastWriteTime, file.Length);
            })
            .OrderByDescending(backup => backup.CreatedAt)
            .ToList();
    }

    public bool DeleteBackup(string backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
        {
            return false;
        }

        File.Delete(backupPath);
        return true;
    }

    private static void EnsureSaveDirectoryExists(Game game)
    {
        if (string.IsNullOrWhiteSpace(game.SavePath))
        {
            throw new InvalidOperationException("当前游戏没有设置存档目录。");
        }

        if (!Directory.Exists(game.SavePath))
        {
            throw new DirectoryNotFoundException($"存档目录不存在：{game.SavePath}");
        }
    }

    private void PruneBackups(Game game)
    {
        var backups = GetBackups(game);
        foreach (var backup in backups.Skip(Math.Max(1, retentionCountProvider())))
        {
            File.Delete(backup.Path);
        }
    }

    private static void ClearDirectory(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly))
        {
            File.Delete(file);
        }

        foreach (var directory in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
        {
            Directory.Delete(directory, true);
        }
    }

    private static bool IsInsideDirectory(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<string> GetBackupDirectories(Game game)
    {
        if (!Directory.Exists(backupRootDirectory))
        {
            return [];
        }

        var suffix = $"-{SafePathSegment.Create(game.Id, "game")}";
        return Directory.EnumerateDirectories(backupRootDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void TryDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary cleanup must not hide the restore result.
        }
    }
}
