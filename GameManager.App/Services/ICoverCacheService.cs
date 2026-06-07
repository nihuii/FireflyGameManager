using GameManager.App.Models;

namespace GameManager.App.Services;

public interface ICoverCacheService
{
    string? Cache(Game game);
}
