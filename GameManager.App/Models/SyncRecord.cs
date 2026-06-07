namespace GameManager.App.Models;

public sealed class SyncRecord
{
    public SyncRecord(
        string id,
        string? gameId,
        string syncType,
        string direction,
        string status,
        DateTime startedAt,
        DateTime? finishedAt,
        string message)
    {
        Id = id;
        GameId = gameId;
        SyncType = syncType;
        Direction = direction;
        Status = status;
        StartedAt = startedAt;
        FinishedAt = finishedAt;
        Message = message;
    }

    public string Id { get; }
    public string? GameId { get; }
    public string SyncType { get; }
    public string Direction { get; }
    public string Status { get; }
    public DateTime StartedAt { get; }
    public DateTime? FinishedAt { get; }
    public string Message { get; }

    public string StartedAtText => StartedAt.ToString("yyyy-MM-dd HH:mm");
}
