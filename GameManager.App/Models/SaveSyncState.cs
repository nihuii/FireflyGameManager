namespace GameManager.App.Models;

public sealed class SaveSyncState
{
    public SaveSyncState(
        string gameId,
        string localHash,
        string remoteHash,
        string lastSyncedHash,
        string status,
        DateTime updatedAt)
    {
        GameId = gameId;
        LocalHash = localHash;
        RemoteHash = remoteHash;
        LastSyncedHash = lastSyncedHash;
        Status = status;
        UpdatedAt = updatedAt;
    }

    public string GameId { get; }
    public string LocalHash { get; }
    public string RemoteHash { get; }
    public string LastSyncedHash { get; }
    public string Status { get; }
    public DateTime UpdatedAt { get; }
}
