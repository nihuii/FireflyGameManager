using System.IO;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class WebDavFullSyncService : IWebDavFullSyncService
{
    private readonly IWebDavManualSyncService manualSyncService;
    private readonly SqliteGameLibraryMergeService gameLibraryMergeService;
    private readonly SaveBackupMergeService saveBackupMergeService;
    private readonly Func<string> createTempRoot;
    private readonly IWebDavGameSyncService? gameSyncService;
    private readonly ISaveManifestService? saveManifestService;
    private readonly string machineId;

    public WebDavFullSyncService(IWebDavManualSyncService manualSyncService)
        : this(
            manualSyncService,
            new SqliteGameLibraryMergeService(),
            new SaveBackupMergeService(),
            () => Path.Combine(Path.GetTempPath(), "FireflyGameManagerSync"),
            null,
            null,
            Environment.MachineName)
    {
    }

    public WebDavFullSyncService(
        IWebDavManualSyncService manualSyncService,
        SqliteGameLibraryMergeService gameLibraryMergeService,
        SaveBackupMergeService saveBackupMergeService,
        Func<string> createTempRoot)
        : this(
            manualSyncService,
            gameLibraryMergeService,
            saveBackupMergeService,
            createTempRoot,
            null,
            null,
            Environment.MachineName)
    {
    }

    public WebDavFullSyncService(
        IWebDavManualSyncService manualSyncService,
        SqliteGameLibraryMergeService gameLibraryMergeService,
        SaveBackupMergeService saveBackupMergeService,
        Func<string> createTempRoot,
        IWebDavGameSyncService? gameSyncService,
        ISaveManifestService? saveManifestService,
        string machineId)
    {
        this.manualSyncService = manualSyncService;
        this.gameLibraryMergeService = gameLibraryMergeService;
        this.saveBackupMergeService = saveBackupMergeService;
        this.createTempRoot = createTempRoot;
        this.gameSyncService = gameSyncService;
        this.saveManifestService = saveManifestService;
        this.machineId = machineId;
    }

    public async Task<WebDavFullSyncResult> SynchronizeAsync(WebDavSettings settings, string databasePath, string saveBackupsDirectory)
    {
        var tempRoot = Path.Combine(createTempRoot(), Guid.NewGuid().ToString("N"));
        var remoteDatabasePath = Path.Combine(tempRoot, "metadata", "app.db");
        var remoteSaveBackupsDirectory = Path.Combine(tempRoot, "SaveBackups");
        Directory.CreateDirectory(Path.GetDirectoryName(remoteDatabasePath)!);
        Directory.CreateDirectory(remoteSaveBackupsDirectory);

        try
        {
            var modernMetadata = gameSyncService is null
                ? Array.Empty<GameCloudMetadata>()
                : await gameSyncService.DownloadGameMetadataAsync(settings);
            var userDownload = await manualSyncService.DownloadUserDataAsync(settings, remoteDatabasePath);
            if (!userDownload.Success)
            {
                return Failure($"同步失败：下载用户信息失败。{userDownload.Message}");
            }

            var backupDownload = await manualSyncService.DownloadSaveBackupsAsync(settings, remoteSaveBackupsDirectory);
            if (!backupDownload.Success)
            {
                return Failure($"同步失败：下载存档备份失败。{backupDownload.Message}");
            }

            var legacyMerge = File.Exists(remoteDatabasePath)
                ? gameLibraryMergeService.MergeRemoteIntoLocal(databasePath, remoteDatabasePath)
                : new GameLibraryMergeResult(0, 0);
            var library = new SqliteGameLibraryService(databasePath, machineId);
            var localBeforeModernMerge = library.GetGames().ToDictionary(game => game.Id, StringComparer.OrdinalIgnoreCase);
            var modernMerge = library.MergeCloudMetadata(modernMetadata);

            if (gameSyncService is not null)
            {
                foreach (var metadata in modernMetadata)
                {
                    var machinePath = await gameSyncService.DownloadMachinePathAsync(settings, metadata.Id, machineId);
                    if (machinePath is not null)
                    {
                        library.ApplyMachinePath(metadata.Id, machinePath);
                    }

                    library.MergePlaySessions(await gameSyncService.DownloadPlaySessionsAsync(settings, metadata));
                    var externalMetadata = await gameSyncService.DownloadExternalMetadataAsync(settings, metadata.Id);
                    if (externalMetadata is not null)
                    {
                        library.ApplyCloudExternalMetadata(externalMetadata);
                    }

                    localBeforeModernMerge.TryGetValue(metadata.Id, out var localBefore);
                    if (CloudCoverMergePolicy.ShouldApply(metadata, localBefore))
                    {
                        if (string.IsNullOrWhiteSpace(metadata.CoverFileName))
                        {
                            library.UpdateCloudCoverPath(metadata.Id, null);
                        }
                        else
                        {
                            var coverDirectory = Path.Combine(Path.GetDirectoryName(databasePath) ?? ".", "CoverCache");
                            var coverPath = await gameSyncService.DownloadCoverAsync(settings, metadata, coverDirectory);
                            if (!string.IsNullOrWhiteSpace(coverPath))
                            {
                                library.UpdateCloudCoverPath(metadata.Id, coverPath);
                            }
                        }
                    }
                }
            }

            var backupMerge = saveBackupMergeService.MergeRemoteIntoLocal(saveBackupsDirectory, remoteSaveBackupsDirectory);
            var userUpload = await manualSyncService.UploadUserDataAsync(settings, databasePath);
            if (!userUpload.Success)
            {
                return Failure($"同步失败：上传用户信息失败。{userUpload.Message}");
            }

            var games = library.GetGames();
            var filteredBackups = PrepareEnabledBackupsForUpload(
                saveBackupsDirectory,
                Path.Combine(tempRoot, "EnabledBackups"),
                games.Where(game => game.SyncEnabled).Select(game => game.Id));
            var backupUpload = await manualSyncService.UploadSaveBackupsAsync(settings, filteredBackups);
            if (!backupUpload.Success)
            {
                return Failure($"同步失败：上传存档备份失败。{backupUpload.Message}");
            }

            if (gameSyncService is not null)
            {
                var indexResult = await gameSyncService.UploadGamesIndexAsync(settings, games);
                if (!indexResult.Success)
                {
                    return Failure(indexResult.Message);
                }

                foreach (var game in games)
                {
                    var latestBackup = game.SyncEnabled ? FindLatestBackup(saveBackupsDirectory, game.Id) : null;
                    var manifest = latestBackup is null ? null : saveManifestService?.CreateFromArchive(latestBackup);
                    var uploadResult = await gameSyncService.UploadGameAsync(
                        settings,
                        game,
                        library.GetPlaySessions(game.Id),
                        machineId,
                        manifest,
                        latestBackup);
                    if (!uploadResult.Success)
                    {
                        return Failure(uploadResult.Message);
                    }

                    var externalMetadata = library.GetExternalMetadataSnapshot(game.Id);
                    if (externalMetadata is not null && ExternalMetadataSyncPolicy.CanUpload(library, game.Id))
                    {
                        var externalUpload = await gameSyncService.UploadExternalMetadataAsync(settings, externalMetadata);
                        if (!externalUpload.Success)
                        {
                            return Failure(externalUpload.Message);
                        }
                    }
                }
            }

            var added = legacyMerge.AddedCount + modernMerge.AddedCount;
            var updated = legacyMerge.UpdatedCount + modernMerge.UpdatedCount;
            return new WebDavFullSyncResult(
                true,
                $"同步完成：游戏新增 {added} 个，更新 {updated} 个；存档新增 {backupMerge.AddedCount} 个，更新 {backupMerge.UpdatedCount} 个",
                added,
                updated,
                backupMerge.AddedCount,
                backupMerge.UpdatedCount);
        }
        catch (Exception ex)
        {
            return Failure($"同步失败：{ex.Message}");
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static WebDavFullSyncResult Failure(string message) =>
        new(false, message, 0, 0, 0, 0);

    private static string? FindLatestBackup(string root, string gameId)
    {
        if (!Directory.Exists(root))
        {
            return null;
        }

        var suffix = $"-{SafePathSegment.Create(gameId, "game")}";
        return Directory.EnumerateDirectories(root)
            .Where(path => Path.GetFileName(path).EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => Directory.EnumerateFiles(path, "*.zip", SearchOption.TopDirectoryOnly))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static string PrepareEnabledBackupsForUpload(string sourceRoot, string destinationRoot, IEnumerable<string> enabledGameIds)
    {
        Directory.CreateDirectory(destinationRoot);
        if (!Directory.Exists(sourceRoot))
        {
            return destinationRoot;
        }

        var suffixes = enabledGameIds.Select(id => $"-{SafePathSegment.Create(id, "game")}").ToList();
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.TopDirectoryOnly)
                     .Where(path => suffixes.Any(suffix => Path.GetFileName(path).EndsWith(suffix, StringComparison.OrdinalIgnoreCase))))
        {
            var targetDirectory = Path.Combine(destinationRoot, Path.GetFileName(directory));
            Directory.CreateDirectory(targetDirectory);
            foreach (var file in Directory.EnumerateFiles(directory, "*.zip", SearchOption.TopDirectoryOnly))
            {
                File.Copy(file, Path.Combine(targetDirectory, Path.GetFileName(file)), true);
            }
        }

        return destinationRoot;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // Temporary cleanup must not hide the synchronization result.
        }
    }
}
