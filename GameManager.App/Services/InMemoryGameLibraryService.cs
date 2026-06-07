using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class InMemoryGameLibraryService : IGameLibraryService
{
    private readonly List<Game> games;

    public InMemoryGameLibraryService()
    {
        games = CreateSampleGames();
    }

    public IReadOnlyList<Game> GetGames()
    {
        return games.ToList();
    }

    public Game AddGame(AddGameRequest request)
    {
        var game = new Game(
            Guid.NewGuid().ToString("N"),
            request.Name.Trim(),
            request.ExecutablePath.Trim(),
            request.GameRootPath.Trim(),
            request.SavePath.Trim(),
            string.IsNullOrWhiteSpace(request.CoverImagePath) ? null : request.CoverImagePath.Trim(),
            TimeSpan.Zero,
            null,
            request.LaunchArguments.Trim(),
            request.RunAsAdministrator);

        games.Add(game);
        return game;
    }

    public bool DeleteGame(string id)
    {
        var index = games.FindIndex(game => game.Id == id);
        if (index < 0)
        {
            return false;
        }

        games.RemoveAt(index);
        return true;
    }

    public Game UpdateGame(UpdateGameRequest request)
    {
        var index = games.FindIndex(game => game.Id == request.Id);
        if (index < 0)
        {
            throw new InvalidOperationException($"Game '{request.Id}' was not found.");
        }

        var existing = games[index];
        var updated = new Game(
            existing.Id,
            request.Name.Trim(),
            request.ExecutablePath.Trim(),
            request.GameRootPath.Trim(),
            request.SavePath.Trim(),
            string.IsNullOrWhiteSpace(request.CoverImagePath) ? null : request.CoverImagePath.Trim(),
            existing.TotalPlayTime,
            existing.LastLaunchTime,
            request.LaunchArguments.Trim(),
            request.RunAsAdministrator);

        games[index] = updated;
        return updated;
    }

    public bool PinGameToTop(string id)
    {
        var index = games.FindIndex(game => game.Id == id);
        if (index <= 0)
        {
            return index == 0;
        }

        var game = games[index];
        games.RemoveAt(index);
        games.Insert(0, game);
        return true;
    }

    public Game RecordLaunchResult(string id, LaunchResult result)
    {
        var index = games.FindIndex(game => game.Id == id);
        if (index < 0)
        {
            throw new InvalidOperationException($"Game '{id}' was not found.");
        }

        var existing = games[index];
        var updated = new Game(
            existing.Id,
            existing.Name,
            existing.ExecutablePath,
            existing.GameRootPath,
            existing.SavePath,
            existing.CoverImagePath,
            existing.TotalPlayTime + result.Duration,
            result.LaunchedAt,
            existing.LaunchArguments,
            existing.RunAsAdministrator);

        games[index] = updated;
        return updated;
    }

    private static List<Game> CreateSampleGames()
    {
        return
        [
            new Game(
                "sample-celeste",
                "Celeste",
                @"D:\Games\Celeste\Celeste.exe",
                @"D:\Games\Celeste",
                @"C:\Users\Public\Saved Games\Celeste",
                null,
                TimeSpan.FromHours(18.5),
                DateTime.Today.AddDays(-2).AddHours(21)),
            new Game(
                "sample-hades",
                "Hades",
                @"D:\Games\Hades\Hades.exe",
                @"D:\Games\Hades",
                @"C:\Users\Public\Saved Games\Hades",
                null,
                TimeSpan.FromHours(42).Add(TimeSpan.FromMinutes(15)),
                DateTime.Today.AddDays(-8).AddHours(20)),
            new Game(
                "sample-gris",
                "Gris",
                @"D:\Games\Gris\Gris.exe",
                @"D:\Games\Gris",
                @"C:\Users\Public\Saved Games\Gris",
                null,
                TimeSpan.FromHours(5).Add(TimeSpan.FromMinutes(40)),
                DateTime.Today.AddMonths(-1).AddHours(19))
        ];
    }
}
