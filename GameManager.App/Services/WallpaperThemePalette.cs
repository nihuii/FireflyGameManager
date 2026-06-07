using System.Windows.Media;

namespace GameManager.App.Services;

public sealed record WallpaperThemePalette(
    Color AppBackground,
    Color Surface,
    Color Panel,
    Color Text,
    Color MutedText,
    Color Accent,
    Color AccentHover,
    Color PrimaryButtonText,
    Color Line);
