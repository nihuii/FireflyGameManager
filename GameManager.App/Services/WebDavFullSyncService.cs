using System.IO;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class WebDavFullSyncService : IWebDavFullSyncService
{
    private readonly IWebDavManualSyncService manualSyncService;
    private readonly SqliteGameLibraryMergeService gameLibraryMergeService;
    private readonly SaveBackupMergeService saveBackupMergeService;
    private readonly Func<string> createTempRoot;

    public WebDavFullSyncService(IWebDavManualSyncService manualSyncService)
        : this(
            manualSyncService,
            new SqliteGameLibraryMergeService(),
            new SaveBackupMergeService(),
            () => Path.Combine(Path.GetTempPath(), "FireflyGameManagerSync"))
    {
    }

    public WebDavFullSyncService(
        IWebDavManualSyncService manualSyncService,
        SqliteGameLibraryMergeService gameLibraryMergeService,
        SaveBackupMergeService saveBackupMergeService,
        Func<string> createTempRoot)
    {
        this.manualSyncService = manualSyncService;
        this.gameLibraryMergeService = gameLibraryMergeService;
        this.saveBackupMergeService = saveBackupMergeService;
        this.createTempRoot = createTempRoot;
    }

    public async Task<WebDavFullSyncResult> SynchronizeAsync(
        WebDavSettings settings,
        string databasePath,
        string saveBackupsDirectory)
    {
        var tempRoot = Path.Combine(createTempRoot(), Guid.NewGuid().ToString("N"));
        var remoteDatabasePath = Path.Combine(tempRoot, "metadata", "app.db");
        var remoteSaveBackupsDirectory = Path.Combine(tempRoot, "SaveBackups");
        Directory.CreateDirectory(Path.GetDirectoryName(remoteDatabasePath)!);
        Directory.CreateDirectory(remoteSaveBackupsDirectory);

        var userDownload = await manualSyncService.DownloadUserDataAsync(settings, remoteDatabasePath);
        if (!userDownload.Success || !File.Exists(remoteDatabasePath))
        {
            return CreateFailure($"同步失败：下载用户信息失败。{userDownload.Message}");
        }

        var backupDownload = await manualSyncService.DownloadSaveBackupsAsync(settings, remoteSaveBackupsDirectory);
        if (!backupDownload.Success)
        {
            return CreateFailure($"同步失败：下载存档备份失败。{backupDownload.Message}");
        }

        var gameMerge = gameLibraryMergeService.MergeRemoteIntoLocal(databasePath, remoteDatabasePath);
        var backupMerge = saveBackupMergeService.MergeRemoteIntoLocal(saveBackupsDirectory, remoteSaveBackupsDirectory);

        var userUpload = await manualSyncService.UploadUserDataAsync(settings, databasePath);
        if (!userUpload.Success)
        {
            return CreateFailure($"同步失败：上传用户信息失败。{userUpload.Message}");
        }

        var backupUpload = await manualSyncService.UploadSaveBackupsAsync(settings, saveBackupsDirectory);
        if (!backupUpload.Success)
        {
            return CreateFailure($"同步失败：上传存档备份失败。{backupUpload.Message}");
        }

        var message = $"同步完成：游戏新增 {gameMerge.AddedCount} 个，更新 {gameMerge.UpdatedCount} 个；存档新增 {backupMerge.AddedCount} 个，更新 {backupMerge.UpdatedCount} 个";
        return new WebDavFullSyncResult(
            true,
            message,
            gameMerge.AddedCount,
            gameMerge.UpdatedCount,
            backupMerge.AddedCount,
            backupMerge.UpdatedCount);
    }

    private static WebDavFullSyncResult CreateFailure(string message)
    {
        return new WebDavFullSyncResult(false, message, 0, 0, 0, 0);
    }
}
