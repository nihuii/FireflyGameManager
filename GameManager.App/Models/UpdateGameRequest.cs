namespace GameManager.App.Models;

public sealed class UpdateGameRequest
{
    public UpdateGameRequest(
        string id,
        string name,
        string executablePath,
        string gameRootPath,
        string savePath,
        string? coverImagePath,
        string launchArguments = "",
        bool runAsAdministrator = false)
    {
        Id = id;
        Name = name;
        ExecutablePath = executablePath;
        GameRootPath = gameRootPath;
        SavePath = savePath;
        CoverImagePath = coverImagePath;
        LaunchArguments = launchArguments;
        RunAsAdministrator = runAsAdministrator;
    }

    public string Id { get; }

    public string Name { get; }

    public string ExecutablePath { get; }

    public string GameRootPath { get; }

    public string SavePath { get; }

    public string? CoverImagePath { get; }

    public string LaunchArguments { get; }

    public bool RunAsAdministrator { get; }
}
