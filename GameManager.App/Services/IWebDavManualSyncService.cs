using GameManager.App.Models;

namespace GameManager.App.Services;

public interface IWebDavManualSyncService
{
    Task<WebDavUploadResult> UploadUserDataAsync(WebDavSettings settings, string databasePath);

    Task<WebDavUploadResult> UploadSaveBackupsAsync(WebDavSettings settings, string saveBackupsDirectory);

    Task<WebDavDownloadResult> DownloadUserDataAsync(WebDavSettings settings, string databasePath);

    Task<WebDavDownloadResult> DownloadSaveBackupsAsync(WebDavSettings settings, string saveBackupsDirectory);
}
