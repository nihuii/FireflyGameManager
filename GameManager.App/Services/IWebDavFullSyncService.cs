using GameManager.App.Models;

namespace GameManager.App.Services;

public interface IWebDavFullSyncService
{
    Task<WebDavFullSyncResult> SynchronizeAsync(
        WebDavSettings settings,
        string databasePath,
        string saveBackupsDirectory);
}
