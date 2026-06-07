using System.Diagnostics;
using System.IO;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class ProcessGameLauncher : IGameLauncher
{
    public async Task<LaunchResult> LaunchAsync(Game game)
    {
        if (!File.Exists(game.ExecutablePath))
        {
            throw new FileNotFoundException("游戏启动文件不存在。", game.ExecutablePath);
        }

        var launchedAt = DateTime.Now;
        var monitoredProcessName = string.IsNullOrWhiteSpace(game.MonitorProcessName)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(game.MonitorProcessName.Trim());
        var existingMonitoredProcessIds = string.IsNullOrWhiteSpace(monitoredProcessName)
            ? []
            : GetProcessIds(monitoredProcessName);
        var startInfo = new ProcessStartInfo
        {
            FileName = game.ExecutablePath,
            WorkingDirectory = Directory.Exists(game.WorkingDirectory)
                ? game.WorkingDirectory
                : Directory.Exists(game.GameRootPath)
                ? game.GameRootPath
                : Path.GetDirectoryName(game.ExecutablePath) ?? string.Empty,
            Arguments = game.LaunchArguments,
            UseShellExecute = true,
            Verb = game.RunAsAdministrator ? "runas" : string.Empty
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动游戏进程。");
        int? exitCode = null;
        if (!string.IsNullOrWhiteSpace(monitoredProcessName))
        {
            var monitoredProcess = await WaitForNewProcessAsync(
                monitoredProcessName,
                existingMonitoredProcessIds,
                TimeSpan.FromSeconds(30));
            if (monitoredProcess is null)
            {
                await process.WaitForExitAsync();
                exitCode = TryGetExitCode(process);
            }
            else
            {
                while (monitoredProcess is not null)
                {
                    using (monitoredProcess)
                    {
                        existingMonitoredProcessIds.Add(monitoredProcess.Id);
                        await monitoredProcess.WaitForExitAsync();
                        exitCode = TryGetExitCode(monitoredProcess);
                    }

                    monitoredProcess = FindNewProcess(monitoredProcessName, existingMonitoredProcessIds);
                }

                exitCode = process.HasExited ? TryGetExitCode(process) ?? exitCode : exitCode;
            }
        }
        else
        {
            await process.WaitForExitAsync();
            exitCode = TryGetExitCode(process);
        }

        var duration = DateTime.Now - launchedAt;
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return new LaunchResult(launchedAt, duration, exitCode);
    }

    private static async Task<Process?> WaitForNewProcessAsync(
        string processName,
        ISet<int> ignoredProcessIds,
        TimeSpan timeout)
    {
        var startedAt = DateTime.UtcNow;
        while (DateTime.UtcNow - startedAt < timeout)
        {
            var process = FindNewProcess(processName, ignoredProcessIds);
            if (process is not null)
            {
                return process;
            }

            await Task.Delay(500);
        }

        return null;
    }

    private static HashSet<int> GetProcessIds(string processName)
    {
        var ids = new HashSet<int>();
        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                ids.Add(process.Id);
            }
        }

        return ids;
    }

    private static Process? FindNewProcess(string processName, ISet<int> ignoredProcessIds)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            if (!ignoredProcessIds.Contains(process.Id))
            {
                return process;
            }

            process.Dispose();
        }

        return null;
    }

    private static int? TryGetExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch
        {
            return null;
        }
    }
}
