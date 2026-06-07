using GameManager.App.Models;

namespace GameManager.App.Services;

public interface IAppearanceSettingsStore
{
    AppearanceSettings Load();

    void Save(AppearanceSettings settings);
}
