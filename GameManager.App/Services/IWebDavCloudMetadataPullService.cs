using GameManager.App.Models;

namespace GameManager.App.Services;

public interface IWebDavCloudMetadataPullService
{
    Task<WebDavGameSyncResult> PullAsync(WebDavSettings settings);
}
