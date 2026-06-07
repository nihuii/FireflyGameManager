namespace GameManager.App.Models;

public sealed class SaveManifest
{
    public SaveManifest(string combinedHash, DateTime createdAtUtc, IReadOnlyList<SaveManifestFile> files)
    {
        CombinedHash = combinedHash;
        CreatedAtUtc = createdAtUtc;
        Files = files;
    }

    public string CombinedHash { get; }

    public DateTime CreatedAtUtc { get; }

    public IReadOnlyList<SaveManifestFile> Files { get; }
}

public sealed class SaveManifestFile
{
    public SaveManifestFile(string relativePath, long sizeBytes, string sha256)
    {
        RelativePath = relativePath;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
    }

    public string RelativePath { get; }

    public long SizeBytes { get; }

    public string Sha256 { get; }
}
