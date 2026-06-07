namespace GameManager.App.Models;

public sealed class LaunchResult
{
    public LaunchResult(DateTime launchedAt, TimeSpan duration)
    {
        LaunchedAt = launchedAt;
        Duration = duration;
    }

    public DateTime LaunchedAt { get; }

    public TimeSpan Duration { get; }
}
