namespace GameManager.App.Services;

public interface IFilePickerService
{
    string? PickExecutableFile();

    string? PickFolder(string title);

    string? PickCoverImage();

    string? PickWallpaperImage();

    string? PickSaveBackupFile(string initialDirectory);

    string? PickExportArchivePath();

    string? PickImportArchiveFile();
}
