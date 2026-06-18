using GameManager.App.Models;

namespace GameManager.App.Services;

public interface IBangumiAccountStore
{
    BangumiAccount? Load();

    void Save(BangumiAccount account);

    void Clear();
}
