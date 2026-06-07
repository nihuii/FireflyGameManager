using GameManager.App.Models;

namespace GameManager.App.Services;

public interface IAppSettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
