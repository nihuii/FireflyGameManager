namespace GameManager.App.Models;

public enum AppCloseBehavior
{
    Exit,
    MinimizeToTray
}

public enum AppPage
{
    Library,
    Sync,
    Settings
}

public enum AppLanguage
{
    SimplifiedChinese,
    English
}

public enum GameSortMode
{
    Manual,
    RecentLaunch,
    Name,
    PlayTime
}

public enum GameCardSize
{
    Compact,
    Standard,
    Large
}

public sealed class AppSettings
{
    public static AppSettings Default => new();

    public bool StartWithWindows { get; set; }

    public bool StartMinimized { get; set; }

    public AppCloseBehavior CloseBehavior { get; set; } = AppCloseBehavior.Exit;

    public bool RememberLastPage { get; set; } = true;

    public AppPage LastPage { get; set; } = AppPage.Library;

    public AppLanguage Language { get; set; } = AppLanguage.SimplifiedChinese;

    public bool MinimizeAfterGameLaunch { get; set; }

    public bool RestoreAfterGameExit { get; set; } = true;

    public bool BackupBeforeGameLaunch { get; set; }

    public GameSortMode DefaultSort { get; set; } = GameSortMode.Manual;

    public GameCardSize CardSize { get; set; } = GameCardSize.Standard;

    public bool ShowPlayTimeOnCards { get; set; }

    public string ScanDirectory { get; set; } = string.Empty;

    public AppSettings Copy()
    {
        return (AppSettings)MemberwiseClone();
    }
}
