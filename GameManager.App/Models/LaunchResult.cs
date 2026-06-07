namespace GameManager.App.Models;

public sealed class LaunchResult
{
    public LaunchResult(DateTime launchedAt, TimeSpan duration, int? exitCode = null)
    {
        LaunchedAt = launchedAt;
        Duration = duration;
        ExitCode = exitCode;
    }

    public DateTime LaunchedAt { get; }

    public TimeSpan Duration { get; }

    public int? ExitCode { get; }
}
