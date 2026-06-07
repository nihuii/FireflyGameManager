using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class NoopSaveSyncCoordinator : ISaveSyncCoordinator
{
    public Task<SaveSyncOperationResult> CheckBeforeLaunchAsync(Game game) =>
        Task.FromResult(new SaveSyncOperationResult(true, false, "未启用自动同步"));

    public Task<SaveSyncOperationResult> SyncAfterExitAsync(Game game) =>
        Task.FromResult(new SaveSyncOperationResult(true, false, "未启用自动同步"));

    public Task<SaveSyncOperationResult> SynchronizeNowAsync(Game game) =>
        Task.FromResult(new SaveSyncOperationResult(true, false, "未配置云存档同步"));

    public Task<SaveSyncOperationResult> ResolveConflictAsync(Game game, SaveConflictResolution resolution) =>
        Task.FromResult(new SaveSyncOperationResult(true, false, "无需解决冲突"));

    public SaveSyncState? GetState(string gameId) => null;
}
