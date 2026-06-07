using GameManager.App.Models;

namespace GameManager.App.Services;

public interface IGameDiscoveryService
{
    IReadOnlyList<AddGameRequest> Discover(string rootDirectory, IEnumerable<string> existingExecutablePaths);
}
