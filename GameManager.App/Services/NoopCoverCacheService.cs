using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class NoopCoverCacheService : ICoverCacheService
{
    public string? Cache(Game game) => game.CoverImagePath;
}
