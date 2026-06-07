using GameManager.App.Models;

namespace GameManager.App.Services;

public interface IGameLibraryService
{
    IReadOnlyList<Game> GetGames();

    Game AddGame(AddGameRequest request);

    bool DeleteGame(string id);

    Game UpdateGame(UpdateGameRequest request);

    bool PinGameToTop(string id);

    Game RecordLaunchResult(string id, LaunchResult result);

    IReadOnlyList<PlaySession> GetPlaySessions(string gameId);
}
