namespace GameManager.App.Models;

public sealed class Game
{
    public Game(
        string id,
        string name,
        string executablePath,
        string gameRootPath,
        string savePath,
        string? coverImagePath,
        TimeSpan totalPlayTime,
        DateTime? lastLaunchTime,
        string launchArguments = "",
        bool runAsAdministrator = false)
    {
        Id = id;
        Name = name;
        ExecutablePath = executablePath;
        GameRootPath = gameRootPath;
        SavePath = savePath;
        CoverImagePath = coverImagePath;
        TotalPlayTime = totalPlayTime;
        LastLaunchTime = lastLaunchTime;
        LaunchArguments = launchArguments;
        RunAsAdministrator = runAsAdministrator;
    }

    public string Id { get; }

    public string Name { get; }

    public string ExecutablePath { get; }

    public string GameRootPath { get; }

    public string SavePath { get; }

    public string? CoverImagePath { get; }

    public TimeSpan TotalPlayTime { get; }

    public DateTime? LastLaunchTime { get; }

    public string LaunchArguments { get; }

    public bool RunAsAdministrator { get; }

    public string PlayTimeDisplay => $"{(int)TotalPlayTime.TotalHours} 小时 {TotalPlayTime.Minutes} 分钟";
}
