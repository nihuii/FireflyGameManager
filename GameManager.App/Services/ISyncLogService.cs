using GameManager.App.Models;

namespace GameManager.App.Services;

public interface ISyncLogService
{
    void Add(string? gameId, string syncType, string direction, string status, string message);

    IReadOnlyList<SyncRecord> GetRecent(int count = 50);
}
