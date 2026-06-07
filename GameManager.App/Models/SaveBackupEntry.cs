namespace GameManager.App.Models;

public sealed class SaveBackupEntry
{
    public SaveBackupEntry(string path, string fileName, DateTime createdAt, long sizeBytes)
    {
        Path = path;
        FileName = fileName;
        CreatedAt = createdAt;
        SizeBytes = sizeBytes;
    }

    public string Path { get; }

    public string FileName { get; }

    public DateTime CreatedAt { get; }

    public long SizeBytes { get; }

    public string CreatedAtText => CreatedAt.ToString("yyyy-MM-dd HH:mm");

    public string SizeText
    {
        get
        {
            if (SizeBytes >= 1024L * 1024L * 1024L)
            {
                return $"{SizeBytes / 1024d / 1024d / 1024d:0.##} GB";
            }

            if (SizeBytes >= 1024L * 1024L)
            {
                return $"{SizeBytes / 1024d / 1024d:0.##} MB";
            }

            if (SizeBytes >= 1024L)
            {
                return $"{SizeBytes / 1024d:0.##} KB";
            }

            return $"{SizeBytes} B";
        }
    }
}
