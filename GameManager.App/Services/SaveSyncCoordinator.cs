using System.IO;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class SaveSyncCoordinator : ISaveSyncCoordinator
{
    private readonly IWebDavSettingsStore webDavSettingsStore;
    private readonly IAppSettingsStore appSettingsStore;
    private readonly IWebDavGameSyncService gameSyncService;
    private readonly ISaveManifestService manifestService;
    private readonly ISaveBackupService backupService;
    private readonly ISaveSyncStateStore stateStore;
    private readonly ISyncLogService logService;
    private readonly string machineId;
    private readonly Func<string, IReadOnlyList<PlaySession>> playSessionsProvider;

    public SaveSyncCoordinator(
        IWebDavSettingsStore webDavSettingsStore,
        IAppSettingsStore appSettingsStore,
        IWebDavGameSyncService gameSyncService,
        ISaveManifestService manifestService,
        ISaveBackupService backupService,
        ISaveSyncStateStore stateStore,
        ISyncLogService logService,
        string machineId,
        Func<string, IReadOnlyList<PlaySession>>? playSessionsProvider = null)
    {
        this.webDavSettingsStore = webDavSettingsStore;
        this.appSettingsStore = appSettingsStore;
        this.gameSyncService = gameSyncService;
        this.manifestService = manifestService;
        this.backupService = backupService;
        this.stateStore = stateStore;
        this.logService = logService;
        this.machineId = machineId;
        this.playSessionsProvider = playSessionsProvider ?? (_ => []);
    }

    public SaveSyncState? GetState(string gameId) => stateStore.Get(gameId);

    public Task<SaveSyncOperationResult> CheckBeforeLaunchAsync(Game game)
    {
        if (!appSettingsStore.Load().AutoSyncBeforeGameLaunch)
        {
            return Task.FromResult(new SaveSyncOperationResult(true, false, "启动前自动同步未启用"));
        }

        return CompareAndSynchronizeAsync(game, allowUpload: false);
    }

    public Task<SaveSyncOperationResult> SyncAfterExitAsync(Game game)
    {
        if (!appSettingsStore.Load().AutoSyncAfterGameExit)
        {
            return Task.FromResult(new SaveSyncOperationResult(true, false, "退出后自动同步未启用"));
        }

        return CompareAndSynchronizeAsync(game, allowUpload: true);
    }

    public Task<SaveSyncOperationResult> SynchronizeNowAsync(Game game)
    {
        return CompareAndSynchronizeAsync(game, allowUpload: true);
    }

    public async Task<SaveSyncOperationResult> ResolveConflictAsync(Game game, SaveConflictResolution resolution)
    {
        try
        {
            return resolution switch
            {
                SaveConflictResolution.UseLocal => await UploadLocalAsync(game),
                SaveConflictResolution.UseCloud => await DownloadCloudAsync(game, restore: true),
                SaveConflictResolution.KeepBoth => await DownloadCloudAsync(game, restore: false),
                _ => PreservePendingDecision(game)
            };
        }
        catch (Exception ex)
        {
            logService.Add(game.Id, "save", "both", "failed", ex.Message);
            return MarkRetryPending(game, ex.Message);
        }
    }

    private async Task<SaveSyncOperationResult> CompareAndSynchronizeAsync(Game game, bool allowUpload)
    {
        if (!game.SyncEnabled || string.IsNullOrWhiteSpace(game.SavePath))
        {
            return new SaveSyncOperationResult(true, false, "当前游戏未启用存档同步");
        }

        try
        {
            var localDirectoryExists = Directory.Exists(game.SavePath);
            var local = manifestService.Create(game.SavePath);
            var remote = await gameSyncService.DownloadSaveManifestAsync(webDavSettingsStore.Load(), game.Id);
            var state = stateStore.Get(game.Id);
            var last = state?.LastSyncedHash ?? string.Empty;
            if (remote is null)
            {
                if (!localDirectoryExists)
                {
                    return SaveState(game.Id, string.Empty, string.Empty, last, "synced",
                        new SaveSyncOperationResult(true, false, "本地与云端均暂无存档"));
                }

                return allowUpload
                    ? await UploadLocalAsync(game)
                    : SaveState(game.Id, local.CombinedHash, string.Empty, last, "local-only",
                        new SaveSyncOperationResult(true, false, "云端暂无存档，游戏退出后可自动上传"));
            }

            if (local.CombinedHash == remote.CombinedHash)
            {
                return SaveState(game.Id, local.CombinedHash, remote.CombinedHash, local.CombinedHash, "synced",
                    new SaveSyncOperationResult(true, false, "本地与云端存档一致"));
            }

            if (string.IsNullOrWhiteSpace(last))
            {
                if (!localDirectoryExists)
                {
                    return PromptCloudDownload(game.Id, string.Empty, remote.CombinedHash, string.Empty);
                }

                return Conflict(game.Id, local.CombinedHash, remote.CombinedHash, "首次同步检测到两份不同存档，请选择保留版本");
            }

            var localChanged = local.CombinedHash != last;
            var remoteChanged = remote.CombinedHash != last;
            if (localChanged && remoteChanged)
            {
                return Conflict(game.Id, local.CombinedHash, remote.CombinedHash, "本地和云端存档都已变化");
            }

            if (remoteChanged)
            {
                return PromptCloudDownload(game.Id, local.CombinedHash, remote.CombinedHash, last);
            }

            return allowUpload
                ? await UploadLocalAsync(game)
                : SaveState(game.Id, local.CombinedHash, remote.CombinedHash, last, "local-newer",
                    new SaveSyncOperationResult(true, false, "本地存档较新，将在游戏退出后上传"));
        }
        catch (Exception ex)
        {
            logService.Add(game.Id, "save", allowUpload ? "upload" : "download", "failed", ex.Message);
            return MarkRetryPending(game, ex.Message);
        }
    }

    private async Task<SaveSyncOperationResult> UploadLocalAsync(Game game)
    {
        var backupPath = await backupService.BackupAsync(game);
        var manifest = manifestService.CreateFromArchive(backupPath);
        var result = await gameSyncService.UploadGameAsync(
            webDavSettingsStore.Load(),
            game,
            playSessionsProvider(game.Id),
            machineId,
            manifest,
            backupPath);
        logService.Add(game.Id, "save", "upload", result.Success ? "success" : "failed", result.Message);
        var localHash = manifestService.Create(game.SavePath).CombinedHash;
        return result.Success && localHash == manifest.CombinedHash
            ? SaveState(game.Id, localHash, manifest.CombinedHash, manifest.CombinedHash, "synced",
                new SaveSyncOperationResult(true, false, "存档已自动上传"))
            : result.Success
            ? SaveState(game.Id, localHash, manifest.CombinedHash, manifest.CombinedHash, "local-newer",
                new SaveSyncOperationResult(true, false, "存档已上传，但本地内容在上传期间再次变化，将在下次操作时继续同步"))
            : SaveState(game.Id, localHash, stateStore.Get(game.Id)?.RemoteHash ?? string.Empty,
                stateStore.Get(game.Id)?.LastSyncedHash ?? string.Empty, "retry-pending",
                new SaveSyncOperationResult(false, false, $"同步失败，已保留待重试状态：{result.Message}"));
    }

    private async Task<SaveSyncOperationResult> DownloadCloudAsync(Game game, bool restore)
    {
        var remoteManifest = await gameSyncService.DownloadSaveManifestAsync(webDavSettingsStore.Load(), game.Id);
        if (remoteManifest is null)
        {
            return MarkRetryPending(game, "云端存档 Manifest 不存在");
        }

        var destination = restore
            ? Path.Combine(Path.GetTempPath(), "FireflyGameManagerSync", $"{SafePathSegment.Create(game.Id, "game")}-{Guid.NewGuid():N}.zip")
            : Path.Combine(backupService.GetBackupDirectory(game), $"{SafePathSegment.Create(game.Id, "game")}-{DateTime.Now:yyyyMMdd-HHmmss}-cloud-conflict.zip");
        try
        {
            if (!await gameSyncService.DownloadLatestSaveAsync(webDavSettingsStore.Load(), game.Id, destination))
            {
                TryDeleteFile(destination);
                return MarkRetryPending(game, "云端最新存档下载失败");
            }

            var downloadedManifest = manifestService.CreateFromArchive(destination);
            if (downloadedManifest.CombinedHash != remoteManifest.CombinedHash)
            {
                TryDeleteFile(destination);
                return MarkRetryPending(game, "云端存档下载后校验失败");
            }

            if (restore)
            {
                await backupService.RestoreAsync(game, destination);
            }

            var localHash = restore ? manifestService.Create(game.SavePath).CombinedHash : LocalHash(game);
            if (restore && localHash != remoteManifest.CombinedHash)
            {
                const string message = "云端存档恢复后校验失败，已保留恢复前备份";
                logService.Add(game.Id, "save", "download", "failed", message);
                var state = stateStore.Get(game.Id);
                return SaveState(
                    game.Id,
                    localHash,
                    remoteManifest.CombinedHash,
                    state?.LastSyncedHash ?? string.Empty,
                    "retry-pending",
                    new SaveSyncOperationResult(false, false, message));
            }

            logService.Add(game.Id, "save", "download", "success", restore ? "云端存档已恢复" : "云端冲突版本已保存为本地备份");
            return SaveState(game.Id, localHash, remoteManifest.CombinedHash, restore ? remoteManifest.CombinedHash : string.Empty,
                restore ? "synced" : "conflict-preserved",
                new SaveSyncOperationResult(true, false, restore ? "云端存档已恢复" : "两份存档均已保留"));
        }
        catch
        {
            if (!restore)
            {
                TryDeleteFile(destination);
            }

            throw;
        }
        finally
        {
            if (restore)
            {
                TryDeleteFile(destination);
            }
        }
    }

    private SaveSyncOperationResult Conflict(string gameId, string localHash, string remoteHash, string message)
    {
        logService.Add(gameId, "save", "both", "conflict", message);
        return SaveState(gameId, localHash, remoteHash, string.Empty, "conflict", new SaveSyncOperationResult(true, true, message));
    }

    private SaveSyncOperationResult PromptCloudDownload(
        string gameId,
        string localHash,
        string remoteHash,
        string lastSyncedHash)
    {
        const string message = "云端存档较新，请确认是否下载后再启动游戏";
        logService.Add(gameId, "save", "download", "pending-confirmation", message);
        return SaveState(
            gameId,
            localHash,
            remoteHash,
            lastSyncedHash,
            "cloud-newer",
            new SaveSyncOperationResult(true, false, message, true));
    }

    private SaveSyncOperationResult PreservePendingDecision(Game game)
    {
        var state = stateStore.Get(game.Id);
        var isCloudNewer = state?.Status == "cloud-newer";
        return SaveState(
            game.Id,
            LocalHash(game),
            state?.RemoteHash ?? string.Empty,
            state?.LastSyncedHash ?? string.Empty,
            isCloudNewer ? "cloud-newer" : "conflict",
            new SaveSyncOperationResult(
                true,
                !isCloudNewer,
                "已保留当前状态，未覆盖任何存档",
                isCloudNewer));
    }

    private SaveSyncOperationResult MarkRetryPending(Game game, string message)
    {
        var state = stateStore.Get(game.Id);
        string localHash;
        try
        {
            localHash = LocalHash(game);
        }
        catch
        {
            localHash = state?.LocalHash ?? string.Empty;
        }

        return SaveState(
            game.Id,
            localHash,
            state?.RemoteHash ?? string.Empty,
            state?.LastSyncedHash ?? string.Empty,
            "retry-pending",
            new SaveSyncOperationResult(false, false, $"同步失败，已保留待重试状态：{message}"));
    }

    private SaveSyncOperationResult SaveState(
        string gameId,
        string localHash,
        string remoteHash,
        string lastSyncedHash,
        string status,
        SaveSyncOperationResult result)
    {
        stateStore.Save(new SaveSyncState(gameId, localHash, remoteHash, lastSyncedHash, status, DateTime.UtcNow));
        return result;
    }

    private string LocalHash(Game game)
    {
        return manifestService.Create(game.SavePath).CombinedHash;
    }

    private static void TryDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary cleanup must not hide the synchronization result.
        }
    }
}
