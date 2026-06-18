using GameManager.App.Models;

namespace GameManager.App.Services;

public interface IWebDavGameSyncService
{
    Task<WebDavGameSyncResult> UploadGamesIndexAsync(WebDavSettings settings, IReadOnlyList<Game> games);

    Task<WebDavGameSyncResult> UploadGameAsync(
        WebDavSettings settings,
        Game game,
        IReadOnlyList<PlaySession> sessions,
        string machineId,
        SaveManifest? saveManifest,
        string? latestBackupPath);

    Task<IReadOnlyList<GameCloudMetadata>> DownloadGameMetadataAsync(WebDavSettings settings);

    Task<WebDavGameSyncResult> UploadExternalMetadataAsync(
        WebDavSettings settings,
        ExternalGameMetadataCloudSnapshot snapshot);

    Task<ExternalGameMetadataCloudSnapshot?> DownloadExternalMetadataAsync(WebDavSettings settings, string gameId);

    Task<IReadOnlyList<PlaySession>> DownloadPlaySessionsAsync(WebDavSettings settings, GameCloudMetadata metadata);

    Task<MachineGamePath?> DownloadMachinePathAsync(WebDavSettings settings, string gameId, string machineId);

    Task<string?> DownloadCoverAsync(WebDavSettings settings, GameCloudMetadata metadata, string coverDirectory);

    Task<SaveManifest?> DownloadSaveManifestAsync(WebDavSettings settings, string gameId);

    Task<bool> DownloadLatestSaveAsync(WebDavSettings settings, string gameId, string destinationPath);
}
