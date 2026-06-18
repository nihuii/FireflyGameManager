using System.IO;

namespace GameManager.App.Services;

public static class AppPaths
{
    public static string DataDirectory
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "FireflyGameManager");
        }
    }

    public static string DatabasePath
    {
        get
        {
            return Path.Combine(DataDirectory, "app.db");
        }
    }

    public static string SaveBackupsDirectory
    {
        get
        {
            return Path.Combine(DataDirectory, "SaveBackups");
        }
    }

    public static string WebDavSettingsPath
    {
        get
        {
            return Path.Combine(DataDirectory, "webdav-settings.json");
        }
    }

    public static string AppearanceSettingsPath
    {
        get
        {
            return Path.Combine(DataDirectory, "appearance-settings.json");
        }
    }

    public static string AppSettingsPath => Path.Combine(DataDirectory, "app-settings.json");

    public static string CoverCacheDirectory => Path.Combine(DataDirectory, "CoverCache");

    public static string MetadataCacheDirectory => Path.Combine(DataDirectory, "MetadataCache");

    public static string BangumiAccountPath => Path.Combine(DataDirectory, "bangumi-account.json");

    public static string MachineIdPath => Path.Combine(DataDirectory, "machine-id.txt");
}
