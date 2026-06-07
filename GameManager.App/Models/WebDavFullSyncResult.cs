namespace GameManager.App.Models;

public sealed class WebDavFullSyncResult
{
    public WebDavFullSyncResult(
        bool success,
        string message,
        int gamesAdded,
        int gamesUpdated,
        int backupsAdded,
        int backupsUpdated)
    {
        Success = success;
        Message = message;
        GamesAdded = gamesAdded;
        GamesUpdated = gamesUpdated;
        BackupsAdded = backupsAdded;
        BackupsUpdated = backupsUpdated;
    }

    public bool Success { get; }

    public string Message { get; }

    public int GamesAdded { get; }

    public int GamesUpdated { get; }

    public int BackupsAdded { get; }

    public int BackupsUpdated { get; }
}
