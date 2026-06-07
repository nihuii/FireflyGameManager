using System.IO;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class LocalCoverCacheService : ICoverCacheService
{
    private readonly string coverDirectory;

    public LocalCoverCacheService(string coverDirectory)
    {
        this.coverDirectory = coverDirectory;
    }

    public string? Cache(Game game)
    {
        if (string.IsNullOrWhiteSpace(game.CoverImagePath) || !File.Exists(game.CoverImagePath))
        {
            return game.CoverImagePath;
        }

        Directory.CreateDirectory(coverDirectory);
        var extension = Path.GetExtension(game.CoverImagePath);
        var destination = Path.Combine(coverDirectory, $"{SafePathSegment.Create(game.Id, "game")}{extension}");
        if (!Path.GetFullPath(game.CoverImagePath).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(game.CoverImagePath, destination, true);
        }

        return destination;
    }
}
