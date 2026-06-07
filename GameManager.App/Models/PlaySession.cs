namespace GameManager.App.Models;

public sealed class PlaySession
{
    public PlaySession(
        string id,
        string gameId,
        string machineId,
        DateTime startedAt,
        DateTime endedAt,
        TimeSpan duration,
        int? exitCode = null,
        bool synced = false)
    {
        Id = id;
        GameId = gameId;
        MachineId = machineId;
        StartedAt = startedAt;
        EndedAt = endedAt;
        Duration = duration;
        ExitCode = exitCode;
        Synced = synced;
    }

    public string Id { get; }

    public string GameId { get; }

    public string MachineId { get; }

    public DateTime StartedAt { get; }

    public DateTime EndedAt { get; }

    public TimeSpan Duration { get; }

    public int? ExitCode { get; }

    public bool Synced { get; }
}
