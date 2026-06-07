using GameManager.App.Models;

namespace GameManager.App.Services;

public interface IAppearanceThemeService
{
    void Apply(AppearanceSettings settings);
}
