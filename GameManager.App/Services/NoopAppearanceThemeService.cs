using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class NoopAppearanceThemeService : IAppearanceThemeService
{
    public void Apply(AppearanceSettings settings)
    {
    }
}
