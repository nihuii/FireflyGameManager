namespace GameManager.App.Services;

public sealed class NoopFilePickerService : IFilePickerService
{
    public string? PickExecutableFile()
    {
        return null;
    }

    public string? PickFolder(string title)
    {
        return null;
    }

    public string? PickCoverImage()
    {
        return null;
    }

    public string? PickWallpaperImage()
    {
        return null;
    }

    public string? PickSaveBackupFile(string initialDirectory)
    {
        return null;
    }

    public string? PickExportArchivePath()
    {
        return null;
    }

    public string? PickImportArchiveFile()
    {
        return null;
    }
}
