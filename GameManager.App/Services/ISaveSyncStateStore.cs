using GameManager.App.Models;

namespace GameManager.App.Services;

public interface ISaveSyncStateStore
{
    SaveSyncState? Get(string gameId);

    void Save(SaveSyncState state);
}
