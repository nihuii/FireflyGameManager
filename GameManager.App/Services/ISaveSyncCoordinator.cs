using GameManager.App.Models;

namespace GameManager.App.Services;

public interface ISaveSyncCoordinator
{
    Task<SaveSyncOperationResult> CheckBeforeLaunchAsync(Game game);

    Task<SaveSyncOperationResult> SyncAfterExitAsync(Game game);

    Task<SaveSyncOperationResult> SynchronizeNowAsync(Game game);

    Task<SaveSyncOperationResult> ResolveConflictAsync(Game game, SaveConflictResolution resolution);

    SaveSyncState? GetState(string gameId);
}
