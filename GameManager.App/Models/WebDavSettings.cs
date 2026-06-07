namespace GameManager.App.Models;

public sealed class WebDavSettings
{
    public WebDavSettings(string serverUrl, string username, string applicationPassword, string remoteDirectory)
    {
        ServerUrl = serverUrl;
        Username = username;
        ApplicationPassword = applicationPassword;
        RemoteDirectory = remoteDirectory;
    }

    public static WebDavSettings Default => new(
        "https://dav.jianguoyun.com/dav/",
        string.Empty,
        string.Empty,
        "FireflyGameManager");

    public string ServerUrl { get; }

    public string Username { get; }

    public string ApplicationPassword { get; }

    public string RemoteDirectory { get; }
}
