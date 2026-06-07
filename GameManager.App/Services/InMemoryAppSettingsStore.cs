using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class InMemoryAppSettingsStore : IAppSettingsStore
{
    private AppSettings settings;

    public InMemoryAppSettingsStore(AppSettings? settings = null)
    {
        this.settings = (settings ?? AppSettings.Default).Copy();
    }

    public AppSettings Load()
    {
        return settings.Copy();
    }

    public void Save(AppSettings settings)
    {
        this.settings = settings.Copy();
    }
}
