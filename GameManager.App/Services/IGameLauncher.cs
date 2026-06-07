using GameManager.App.Models;

namespace GameManager.App.Services;

public interface IGameLauncher
{
    Task<LaunchResult> LaunchAsync(Game game);
}
