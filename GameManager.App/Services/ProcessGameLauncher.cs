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
        var startInfo = new ProcessStartInfo
        {
            FileName = game.ExecutablePath,
            WorkingDirectory = Directory.Exists(game.GameRootPath)
                ? game.GameRootPath
                : Path.GetDirectoryName(game.ExecutablePath) ?? string.Empty,
            Arguments = game.LaunchArguments,
            UseShellExecute = true,
            Verb = game.RunAsAdministrator ? "runas" : string.Empty
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动游戏进程。");
        await process.WaitForExitAsync();

        var duration = DateTime.Now - launchedAt;
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return new LaunchResult(launchedAt, duration);
    }
}
