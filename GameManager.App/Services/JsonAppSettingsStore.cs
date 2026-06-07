using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private readonly string path;
    private readonly JsonSerializerOptions options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonAppSettingsStore(string path)
    {
        this.path = path;
    }

    public AppSettings Load()
    {
        if (!File.Exists(path))
        {
            return AppSettings.Default;
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), options)
                ?? AppSettings.Default;
        }
        catch (JsonException)
        {
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(settings, options));
    }
}
