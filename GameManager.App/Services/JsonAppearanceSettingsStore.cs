using System.IO;
using System.Text.Json;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class JsonAppearanceSettingsStore : IAppearanceSettingsStore
{
    private readonly string path;

    public JsonAppearanceSettingsStore(string path)
    {
        this.path = path;
    }

    public AppearanceSettings Load()
    {
        if (!File.Exists(path))
        {
            return AppearanceSettings.Default;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<AppearanceSettingsDto>(File.ReadAllText(path));
            return dto is null
                ? AppearanceSettings.Default
                : new AppearanceSettings(dto.WallpaperPath ?? string.Empty, dto.IsTransparentUi);
        }
        catch (JsonException)
        {
            return AppearanceSettings.Default;
        }
    }

    public void Save(AppearanceSettings settings)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(new AppearanceSettingsDto
        {
            WallpaperPath = settings.WallpaperPath,
            IsTransparentUi = settings.IsTransparentUi
        }, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(path, json);
    }

    private sealed class AppearanceSettingsDto
    {
        public string? WallpaperPath { get; set; }

        public bool IsTransparentUi { get; set; }
    }
}
