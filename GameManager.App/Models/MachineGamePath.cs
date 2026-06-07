namespace GameManager.App.Models;

public sealed class MachineGamePath
{
    public string MachineId { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string GameRootPath { get; set; } = string.Empty;
    public string SavePath { get; set; } = string.Empty;
    public string LaunchArguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string MonitorProcessName { get; set; } = string.Empty;
    public bool? RunAsAdministrator { get; set; }
    public bool? SyncEnabled { get; set; }
}
