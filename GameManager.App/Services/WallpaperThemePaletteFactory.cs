using System.Windows.Media;

namespace GameManager.App.Services;

public static class WallpaperThemePaletteFactory
{
    private static readonly Color DefaultDominant = Color.FromRgb(106, 132, 116);
    private static readonly Color DefaultAccent = Color.FromRgb(47, 111, 84);

    public static WallpaperThemePalette CreateFromColors(IEnumerable<Color> colors, bool isTransparentUi)
    {
        var samples = colors.Where(color => color.A > 0).ToArray();
        if (samples.Length == 0)
        {
            samples = [DefaultDominant, DefaultAccent];
        }

        var groups = samples
            .GroupBy(color => (color.R / 32, color.G / 32, color.B / 32))
            .Select(group => new ColorGroup(Average(group), group.Count()))
            .ToArray();

        var dominant = groups
            .OrderByDescending(group => group.Count)
            .ThenByDescending(group => Saturation(group.Color))
            .First()
            .Color;
        var minimumMeaningfulCount = Math.Max(1, samples.Length / 1000);
        var colorfulGroups = groups
            .Where(group => Saturation(group.Color) >= 0.22 && group.Count >= minimumMeaningfulCount)
            .ToArray();
        var accent = (colorfulGroups.Length > 0 ? colorfulGroups : groups)
            .OrderByDescending(group => AccentScore(group, dominant))
            .First()
            .Color;
        accent = NormalizeAccent(accent);
        var themeTint = Blend(dominant, accent, 0.38);

        var isDark = RelativeLuminance(dominant) < 0.46;
        var text = isDark ? Colors.White : Color.FromRgb(18, 25, 21);
        var mutedText = isDark
            ? Blend(text, dominant, 0.35)
            : Blend(text, dominant, 0.46);
        var primaryButtonText = RelativeLuminance(accent) > 0.56 ? Colors.Black : Colors.White;
        var accentHover = Blend(accent, isDark ? Colors.White : Colors.Black, isDark ? 0.14 : 0.18);

        if (isTransparentUi)
        {
            var surfaceBase = Blend(themeTint, isDark ? Colors.Black : Colors.White, isDark ? 0.35 : 0.64);
            var panelBase = Blend(themeTint, isDark ? Colors.Black : Colors.White, isDark ? 0.20 : 0.76);
            var lineBase = Blend(themeTint, text, isDark ? 0.34 : 0.22);
            return new WallpaperThemePalette(
                WithAlpha(themeTint, 22),
                WithAlpha(surfaceBase, 154),
                WithAlpha(panelBase, 184),
                text,
                mutedText,
                accent,
                accentHover,
                primaryButtonText,
                WithAlpha(lineBase, 142));
        }

        var appBackground = Blend(themeTint, isDark ? Colors.Black : Colors.White, isDark ? 0.70 : 0.84);
        var surface = Blend(themeTint, isDark ? Colors.Black : Colors.White, isDark ? 0.57 : 0.91);
        var panel = Blend(themeTint, isDark ? Colors.Black : Colors.White, isDark ? 0.46 : 0.97);
        var line = Blend(themeTint, text, isDark ? 0.28 : 0.17);
        return new WallpaperThemePalette(
            appBackground,
            surface,
            panel,
            text,
            mutedText,
            accent,
            accentHover,
            primaryButtonText,
            line);
    }

    private static double AccentScore(ColorGroup group, Color dominant)
    {
        var saturation = Saturation(group.Color);
        var distance = ColorDistance(group.Color, dominant);
        var luminance = RelativeLuminance(group.Color);
        var readableLuminance = luminance is > 0.15 and < 0.82 ? 1.0 : 0.62;
        return Math.Sqrt(group.Count) * (0.30 + (saturation * 2.5)) * (0.72 + distance) * readableLuminance;
    }

    private static Color NormalizeAccent(Color color)
    {
        var luminance = RelativeLuminance(color);
        if (luminance < 0.16)
        {
            return Blend(color, Colors.White, 0.34);
        }

        if (luminance > 0.76)
        {
            return Blend(color, Colors.Black, 0.30);
        }

        if (Saturation(color) < 0.18)
        {
            return Blend(color, DefaultAccent, 0.52);
        }

        return color;
    }

    private static Color Average(IEnumerable<Color> colors)
    {
        var samples = colors.ToArray();
        return Color.FromRgb(
            (byte)samples.Average(color => color.R),
            (byte)samples.Average(color => color.G),
            (byte)samples.Average(color => color.B));
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static Color Blend(Color first, Color second, double secondWeight)
    {
        var firstWeight = 1 - secondWeight;
        return Color.FromRgb(
            (byte)((first.R * firstWeight) + (second.R * secondWeight)),
            (byte)((first.G * firstWeight) + (second.G * secondWeight)),
            (byte)((first.B * firstWeight) + (second.B * secondWeight)));
    }

    private static double RelativeLuminance(Color color)
    {
        return ((0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B)) / 255;
    }

    private static double Saturation(Color color)
    {
        var maximum = Math.Max(color.R, Math.Max(color.G, color.B));
        var minimum = Math.Min(color.R, Math.Min(color.G, color.B));
        return maximum == 0 ? 0 : (maximum - minimum) / (double)maximum;
    }

    private static double ColorDistance(Color first, Color second)
    {
        var red = first.R - second.R;
        var green = first.G - second.G;
        var blue = first.B - second.B;
        return Math.Sqrt((red * red) + (green * green) + (blue * blue)) / 441.67;
    }

    private sealed record ColorGroup(Color Color, int Count);
}
