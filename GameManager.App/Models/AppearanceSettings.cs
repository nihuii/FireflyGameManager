namespace GameManager.App.Models;

public sealed class AppearanceSettings
{
    public AppearanceSettings(string wallpaperPath, bool isTransparentUi)
    {
        WallpaperPath = wallpaperPath;
        IsTransparentUi = isTransparentUi;
    }

    public static AppearanceSettings Default => new(string.Empty, false);

    public string WallpaperPath { get; }

    public bool IsTransparentUi { get; }
}
