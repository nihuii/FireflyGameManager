using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class SaveManifestService : ISaveManifestService
{
    public SaveManifest Create(string saveDirectory)
    {
        if (string.IsNullOrWhiteSpace(saveDirectory) || !Directory.Exists(saveDirectory))
        {
            return new SaveManifest(ComputeHash([]), DateTime.UtcNow, []);
        }

        var root = Path.GetFullPath(saveDirectory);
        var files = Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => CreateFileEntry(root, path))
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToList();
        var combinedLines = files.Select(file => $"{file.RelativePath}\n{file.SizeBytes}\n{file.Sha256}\n");
        return new SaveManifest(ComputeHash(combinedLines), DateTime.UtcNow, files);
    }

    public SaveManifest CreateFromArchive(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            return new SaveManifest(ComputeHash([]), DateTime.UtcNow, []);
        }

        using var archive = ZipFile.OpenRead(archivePath);
        var files = archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name))
            .Select(CreateArchiveEntry)
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToList();
        var combinedLines = files.Select(file => $"{file.RelativePath}\n{file.SizeBytes}\n{file.Sha256}\n");
        return new SaveManifest(ComputeHash(combinedLines), DateTime.UtcNow, files);
    }

    private static SaveManifestFile CreateFileEntry(string root, string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        var relativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
        return new SaveManifestFile(relativePath, stream.Length, hash);
    }

    private static SaveManifestFile CreateArchiveEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        return new SaveManifestFile(entry.FullName.Replace('\\', '/'), entry.Length, hash);
    }

    private static string ComputeHash(IEnumerable<string> values)
    {
        var bytes = Encoding.UTF8.GetBytes(string.Concat(values));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
