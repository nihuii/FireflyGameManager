using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class DesignGameLibraryService : IGameLibraryService
{
    private readonly InMemoryGameLibraryService inner = new();

    public IReadOnlyList<Game> GetGames()
    {
        return inner.GetGames();
    }

    public Game AddGame(AddGameRequest request)
    {
        return inner.AddGame(request);
    }

    public bool DeleteGame(string id)
    {
        return inner.DeleteGame(id);
    }

    public Game UpdateGame(UpdateGameRequest request)
    {
        return inner.UpdateGame(request);
    }

    public bool PinGameToTop(string id)
    {
        return inner.PinGameToTop(id);
    }

    public Game RecordLaunchResult(string id, LaunchResult result)
    {
        return inner.RecordLaunchResult(id, result);
    }
}
