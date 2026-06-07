using System.IO;
using System.Text.Json;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class JsonWebDavSettingsStore : IWebDavSettingsStore
{
    private readonly string path;
    private readonly ISecretProtector secretProtector;

    public JsonWebDavSettingsStore(string path, ISecretProtector? secretProtector = null)
    {
        this.path = path;
        this.secretProtector = secretProtector ?? new DpapiSecretProtector();
    }

    public WebDavSettings Load()
    {
        if (!File.Exists(path))
        {
            return WebDavSettings.Default;
        }

        WebDavSettingsDto? settings;
        try
        {
            settings = JsonSerializer.Deserialize<WebDavSettingsDto>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return WebDavSettings.Default;
        }

        if (settings is null)
        {
            return WebDavSettings.Default;
        }

        var password = !string.IsNullOrWhiteSpace(settings.EncryptedApplicationPassword)
            ? TryUnprotect(settings.EncryptedApplicationPassword)
            : settings.ApplicationPassword ?? string.Empty;
        var result = new WebDavSettings(
            settings.ServerUrl ?? WebDavSettings.Default.ServerUrl,
            settings.Username ?? string.Empty,
            password,
            settings.RemoteDirectory ?? WebDavSettings.Default.RemoteDirectory);
        if (!string.IsNullOrWhiteSpace(settings.ApplicationPassword) &&
            string.IsNullOrWhiteSpace(settings.EncryptedApplicationPassword))
        {
            Save(result);
        }

        return result;
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
            EncryptedApplicationPassword = secretProtector.Protect(settings.ApplicationPassword),
            RemoteDirectory = settings.RemoteDirectory
        }, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(path, json);
    }

    private sealed class WebDavSettingsDto
    {
        public string? ServerUrl { get; set; }

        public string? Username { get; set; }

        public string? ApplicationPassword { get; set; }

        public string? EncryptedApplicationPassword { get; set; }

        public string? RemoteDirectory { get; set; }
    }

    private string TryUnprotect(string protectedValue)
    {
        try
        {
            return secretProtector.Unprotect(protectedValue);
        }
        catch
        {
            return string.Empty;
        }
    }
}
