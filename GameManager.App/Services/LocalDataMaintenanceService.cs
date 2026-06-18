using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace GameManager.App.Services;

public sealed class LocalDataMaintenanceService : IDataMaintenanceService
{
    public LocalDataMaintenanceService(string dataDirectory)
    {
        DataDirectory = dataDirectory;
    }

    public string DataDirectory { get; }

    public void OpenDataDirectory()
    {
        Directory.CreateDirectory(DataDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", DataDirectory) { UseShellExecute = true });
    }

    public void Export(string destinationZipPath)
    {
        if (string.IsNullOrWhiteSpace(destinationZipPath))
        {
            throw new ArgumentException("Export path is required.", nameof(destinationZipPath));
        }

        Directory.CreateDirectory(DataDirectory);
        var destinationDirectory = Path.GetDirectoryName(destinationZipPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        if (File.Exists(destinationZipPath))
        {
            File.Delete(destinationZipPath);
        }

        var destinationFullPath = Path.GetFullPath(destinationZipPath);
        using var archive = ZipFile.Open(destinationZipPath, ZipArchiveMode.Create);
        foreach (var filePath in Directory.EnumerateFiles(DataDirectory, "*", SearchOption.AllDirectories))
        {
            if (string.Equals(Path.GetFullPath(filePath), destinationFullPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(filePath), "bangumi-account.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entryName = Path.GetRelativePath(DataDirectory, filePath);
            archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
        }
    }

    public void Import(string sourceZipPath)
    {
        if (!File.Exists(sourceZipPath))
        {
            throw new FileNotFoundException("Import archive does not exist.", sourceZipPath);
        }

        Directory.CreateDirectory(DataDirectory);
        ZipFile.ExtractToDirectory(sourceZipPath, DataDirectory, true);
    }

    public int ClearInvalidBackups(IEnumerable<string> validGameIds)
    {
        var backupDirectory = Path.Combine(DataDirectory, "SaveBackups");
        if (!Directory.Exists(backupDirectory))
        {
            return 0;
        }

        var ids = validGameIds.Select(id => SafePathSegment.Create(id, "game")).ToArray();
        var removed = 0;
        foreach (var directory in Directory.EnumerateDirectories(backupDirectory))
        {
            var name = Path.GetFileName(directory);
            if (ids.Any(id => name.EndsWith($"-{id}", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            Directory.Delete(directory, true);
            removed++;
        }

        foreach (var zipPath in Directory.EnumerateFiles(backupDirectory, "*.zip", SearchOption.AllDirectories).ToList())
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                _ = archive.Entries.Count;
            }
            catch (InvalidDataException)
            {
                File.Delete(zipPath);
                removed++;
            }
        }

        return removed;
    }

    public void ClearCoverCache()
    {
        var coverCache = Path.Combine(DataDirectory, "CoverCache");
        if (!Directory.Exists(coverCache))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(coverCache, "*", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }

        foreach (var directory in Directory.EnumerateDirectories(coverCache).OrderByDescending(path => path.Length))
        {
            Directory.Delete(directory, true);
        }
    }
}
