namespace GameManager.App.Models;

public enum BangumiCollectionType
{
    None = 0,
    Wish = 1,
    Collect = 2,
    Doing = 3,
    OnHold = 4,
    Dropped = 5
}

public sealed record BangumiCollectionState(
    string GameId,
    string SubjectId,
    string Username,
    BangumiCollectionType Type,
    int Rating,
    string Comment,
    DateTime? RemoteUpdatedAtUtc,
    DateTime LastSyncedAtUtc);
