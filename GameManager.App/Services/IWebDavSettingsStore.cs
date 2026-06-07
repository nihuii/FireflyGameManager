using GameManager.App.Models;

namespace GameManager.App.Services;

public interface IWebDavSettingsStore
{
    WebDavSettings Load();

    void Save(WebDavSettings settings);
}
