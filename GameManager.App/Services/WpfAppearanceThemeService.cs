using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class WpfAppearanceThemeService : IAppearanceThemeService
{
    public void Apply(AppearanceSettings settings)
    {
        var samples = ReadWallpaperColors(settings.WallpaperPath);
        var palette = WallpaperThemePaletteFactory.CreateFromColors(samples, settings.IsTransparentUi);

        SetThemeResource("AppBackgroundColor", "AppBackgroundBrush", palette.AppBackground);
        SetThemeResource("SurfaceColor", "SurfaceBrush", palette.Surface);
        SetThemeResource("PanelColor", "PanelBrush", palette.Panel);
        SetThemeResource("TextColor", "TextBrush", palette.Text);
        SetThemeResource("MutedTextColor", "MutedTextBrush", palette.MutedText);
        SetThemeResource("AccentColor", "AccentBrush", palette.Accent);
        SetThemeResource("AccentHoverColor", "AccentHoverBrush", palette.AccentHover);
        SetThemeResource("PrimaryButtonTextColor", "PrimaryButtonTextBrush", palette.PrimaryButtonText);
        SetThemeResource("LineColor", "LineBrush", palette.Line);
    }

    private static IReadOnlyList<Color> ReadWallpaperColors(string wallpaperPath)
    {
        if (string.IsNullOrWhiteSpace(wallpaperPath) || !File.Exists(wallpaperPath))
        {
            return [];
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(Path.GetFullPath(wallpaperPath));
            bitmap.DecodePixelWidth = 96;
            bitmap.EndInit();
            bitmap.Freeze();

            var source = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var stride = source.PixelWidth * 4;
            var pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);

            var colors = new List<Color>(source.PixelWidth * source.PixelHeight);
            for (var index = 0; index < pixels.Length; index += 4)
            {
                if (pixels[index + 3] < 32)
                {
                    continue;
                }

                colors.Add(Color.FromRgb(pixels[index + 2], pixels[index + 1], pixels[index]));
            }

            return colors;
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static void SetThemeResource(string colorKey, string brushKey, Color color)
    {
        if (Application.Current is not null)
        {
            Application.Current.Resources[colorKey] = color;
            Application.Current.Resources[brushKey] = new SolidColorBrush(color);
        }
    }
}
