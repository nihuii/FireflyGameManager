using System.IO;

namespace GameManager.App.Services;

public static class SafePathSegment
{
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string Create(string? value, string fallback = "item")
    {
        var invalidChars = Path.GetInvalidFileNameChars()
            .Concat(['/', '\\'])
            .ToHashSet();
        var sanitized = new string((value ?? string.Empty)
            .Select(character => invalidChars.Contains(character) ? '_' : character)
            .ToArray())
            .Trim()
            .TrimEnd('.');

        if (string.IsNullOrWhiteSpace(sanitized) || sanitized is "." or "..")
        {
            return fallback;
        }

        var deviceName = Path.GetFileNameWithoutExtension(sanitized);
        return ReservedDeviceNames.Contains(deviceName) ? "_" + sanitized : sanitized;
    }
}
