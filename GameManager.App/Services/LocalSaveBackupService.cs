using System.IO;
using System.IO.Compression;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class LocalSaveBackupService : ISaveBackupService
{
    private readonly string backupRootDirectory;

    public LocalSaveBackupService(string backupRootDirectory)
    {
        this.backupRootDirectory = backupRootDirectory;
    }

    public Task<string> BackupAsync(Game game)
    {
        return Task.Run(() =>
        {
            EnsureSaveDirectoryExists(game);
            var backupDirectory = GetBackupDirectory(game);
            Directory.CreateDirectory(backupDirectory);

            var fileName = $"{SanitizeFileName(game.Name)}-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
            var backupPath = Path.Combine(backupDirectory, fileName);
            ZipFile.CreateFromDirectory(game.SavePath, backupPath, CompressionLevel.Optimal, false);
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

            Directory.CreateDirectory(game.SavePath);
            ZipFile.ExtractToDirectory(backupPath, game.SavePath, true);
        });
    }

    public string GetBackupDirectory(Game game)
    {
        var gameDirectoryName = $"{SanitizeFileName(game.Name)}-{game.Id}";
        return Path.Combine(backupRootDirectory, gameDirectoryName);
    }

    public IReadOnlyList<SaveBackupEntry> GetBackups(Game game)
    {
        var backupDirectory = GetBackupDirectory(game);
        if (!Directory.Exists(backupDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(backupDirectory, "*.zip", SearchOption.TopDirectoryOnly)
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

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Game" : sanitized;
    }
}
