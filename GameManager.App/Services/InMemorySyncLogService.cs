using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class InMemorySyncLogService : ISyncLogService
{
    private readonly List<SyncRecord> records = [];

    public void Add(string? gameId, string syncType, string direction, string status, string message)
    {
        var now = DateTime.Now;
        records.Insert(0, new SyncRecord(
            Guid.NewGuid().ToString("N"),
            gameId,
            syncType,
            direction,
            status,
            now,
            now,
            message));
    }

    public IReadOnlyList<SyncRecord> GetRecent(int count = 50)
    {
        return records.Take(Math.Max(1, count)).ToList();
    }
}
