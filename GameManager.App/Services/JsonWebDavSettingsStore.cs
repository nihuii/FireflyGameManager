using System.IO;
using System.Text.Json;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class JsonWebDavSettingsStore : IWebDavSettingsStore
{
    private readonly string path;

    public JsonWebDavSettingsStore(string path)
    {
        this.path = path;
    }

    public WebDavSettings Load()
    {
        if (!File.Exists(path))
        {
            return WebDavSettings.Default;
        }

        var json = File.ReadAllText(path);
        var settings = JsonSerializer.Deserialize<WebDavSettingsDto>(json);
        if (settings is null)
        {
            return WebDavSettings.Default;
        }

        return new WebDavSettings(
            settings.ServerUrl ?? WebDavSettings.Default.ServerUrl,
            settings.Username ?? string.Empty,
            settings.ApplicationPassword ?? string.Empty,
            settings.RemoteDirectory ?? WebDavSettings.Default.RemoteDirectory);
    }

    public void Save(WebDavSettings settings)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(new WebDavSettingsDto
        {
            ServerUrl = settings.ServerUrl,
            Username = settings.Username,
            ApplicationPassword = settings.ApplicationPassword,
            RemoteDirectory = settings.RemoteDirectory
        }, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(path, json);
    }

    private sealed class WebDavSettingsDto
    {
        public string? ServerUrl { get; set; }

        public string? Username { get; set; }

        public string? ApplicationPassword { get; set; }

        public string? RemoteDirectory { get; set; }
    }
}
