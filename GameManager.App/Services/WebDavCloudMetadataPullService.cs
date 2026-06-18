using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class WebDavCloudMetadataPullService : IWebDavCloudMetadataPullService
{
    private readonly SqliteGameLibraryService library;
    private readonly IWebDavGameSyncService gameSyncService;
    private readonly ISyncLogService syncLogService;
    private readonly string machineId;
    private readonly string coverDirectory;

    public WebDavCloudMetadataPullService(
        SqliteGameLibraryService library,
        IWebDavGameSyncService gameSyncService,
        ISyncLogService syncLogService,
        string machineId,
        string coverDirectory)
    {
        this.library = library;
        this.gameSyncService = gameSyncService;
        this.syncLogService = syncLogService;
        this.machineId = machineId;
        this.coverDirectory = coverDirectory;
    }

    public async Task<WebDavGameSyncResult> PullAsync(WebDavSettings settings)
    {
        try
        {
            var localBeforeMerge = library.GetGames().ToDictionary(game => game.Id, StringComparer.OrdinalIgnoreCase);
            var metadata = await gameSyncService.DownloadGameMetadataAsync(settings);
            var merge = library.MergeCloudMetadata(metadata);
            var failedGames = 0;
            foreach (var gameMetadata in metadata)
            {
                try
                {
                    var machinePath = await gameSyncService.DownloadMachinePathAsync(settings, gameMetadata.Id, machineId);
                    if (machinePath is not null)
                    {
                        library.ApplyMachinePath(gameMetadata.Id, machinePath);
                    }

                    library.MergePlaySessions(await gameSyncService.DownloadPlaySessionsAsync(settings, gameMetadata));
                    var externalMetadata = await gameSyncService.DownloadExternalMetadataAsync(settings, gameMetadata.Id);
                    if (externalMetadata is not null)
                    {
                        var externalMerge = library.ApplyCloudExternalMetadata(externalMetadata);
                        if (externalMerge.IsConflict)
                        {
                            syncLogService.Add(gameMetadata.Id, "external-metadata", "download", "conflict", externalMerge.Message);
                        }
                    }

                    localBeforeMerge.TryGetValue(gameMetadata.Id, out var localGame);
                    if (CloudCoverMergePolicy.ShouldApply(gameMetadata, localGame))
                    {
                        if (string.IsNullOrWhiteSpace(gameMetadata.CoverFileName))
                        {
                            library.UpdateCloudCoverPath(gameMetadata.Id, null);
                        }
                        else
                        {
                            var coverPath = await gameSyncService.DownloadCoverAsync(settings, gameMetadata, coverDirectory);
                            if (!string.IsNullOrWhiteSpace(coverPath))
                            {
                                library.UpdateCloudCoverPath(gameMetadata.Id, coverPath);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    failedGames++;
                    syncLogService.Add(gameMetadata.Id, "metadata", "download", "failed", ex.Message);
                }
            }

            var message = metadata.Count == 0
                ? "启动同步完成：云端暂无 V2 游戏信息"
                : $"启动同步完成：新增 {merge.AddedCount} 个，更新 {merge.UpdatedCount} 个，单游戏失败 {failedGames} 个";
            syncLogService.Add(null, "metadata", "download", failedGames == 0 ? "success" : "partial", message);
            return new WebDavGameSyncResult(true, message);
        }
        catch (Exception ex)
        {
            var message = $"启动同步失败，不影响本地使用：{ex.Message}";
            syncLogService.Add(null, "metadata", "download", "failed", message);
            return new WebDavGameSyncResult(false, message);
        }
    }
}
