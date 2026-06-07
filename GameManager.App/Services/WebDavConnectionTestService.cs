using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class WebDavConnectionTestService : IWebDavConnectionTester
{
    private readonly Func<HttpClient> createClient;

    public WebDavConnectionTestService()
        : this(() => new HttpClient())
    {
    }

    public WebDavConnectionTestService(Func<HttpClient> createClient)
    {
        this.createClient = createClient;
    }

    public async Task<WebDavConnectionTestResult> TestConnectionAsync(WebDavSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ServerUrl) ||
            string.IsNullOrWhiteSpace(settings.Username) ||
            string.IsNullOrWhiteSpace(settings.ApplicationPassword))
        {
            return new WebDavConnectionTestResult(false, "请先填写服务器地址、账号和应用密码。");
        }

        using var client = createClient();
        var remoteUri = BuildRemoteUri(settings);

        try
        {
            using var response = await SendWebDavRequestAsync(client, "PROPFIND", remoteUri, settings);
            if (IsSuccess(response.StatusCode))
            {
                return new WebDavConnectionTestResult(true, "连接成功");
            }

            if (response.StatusCode == HttpStatusCode.NotFound && HasRemoteDirectory(settings))
            {
                return await TryCreateRemoteDirectoryAsync(client, remoteUri, settings);
            }

            return CreateFailureResult(response.StatusCode);
        }
        catch (Exception ex)
        {
            return new WebDavConnectionTestResult(false, $"连接失败：{ex.Message}");
        }
    }

    private static async Task<WebDavConnectionTestResult> TryCreateRemoteDirectoryAsync(
        HttpClient client,
        Uri remoteUri,
        WebDavSettings settings)
    {
        using var response = await SendWebDavRequestAsync(client, "MKCOL", remoteUri, settings);
        if (response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.MethodNotAllowed ||
            IsSuccess(response.StatusCode))
        {
            return new WebDavConnectionTestResult(true, "连接成功，已创建远程目录");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new WebDavConnectionTestResult(false, "连接失败：账号或应用密码不正确。");
        }

        return new WebDavConnectionTestResult(false, $"远程目录不存在，且创建失败：服务器返回 {(int)response.StatusCode}。");
    }

    private static async Task<HttpResponseMessage> SendWebDavRequestAsync(
        HttpClient client,
        string method,
        Uri uri,
        WebDavSettings settings)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), uri);
        request.Headers.Authorization = CreateBasicAuth(settings);
        if (method == "PROPFIND")
        {
            request.Headers.TryAddWithoutValidation("Depth", "0");
        }

        return await client.SendAsync(request);
    }

    private static WebDavConnectionTestResult CreateFailureResult(HttpStatusCode statusCode)
    {
        if (statusCode == HttpStatusCode.Unauthorized)
        {
            return new WebDavConnectionTestResult(false, "连接失败：账号或应用密码不正确。");
        }

        if (statusCode == HttpStatusCode.NotFound)
        {
            return new WebDavConnectionTestResult(false, "连接失败：远程目录不存在。");
        }

        return new WebDavConnectionTestResult(false, $"连接失败：服务器返回 {(int)statusCode}。");
    }

    private static bool IsSuccess(HttpStatusCode statusCode)
    {
        return ((int)statusCode >= 200 && (int)statusCode <= 299) || statusCode == (HttpStatusCode)207;
    }

    private static bool HasRemoteDirectory(WebDavSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.RemoteDirectory.Trim().Trim('/'));
    }

    private static Uri BuildRemoteUri(WebDavSettings settings)
    {
        var baseUri = settings.ServerUrl.EndsWith("/", StringComparison.Ordinal)
            ? settings.ServerUrl
            : settings.ServerUrl + "/";
        var remoteDirectory = settings.RemoteDirectory.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(remoteDirectory))
        {
            return new Uri(baseUri);
        }

        var escapedSegments = remoteDirectory
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString);
        return new Uri(baseUri + string.Join("/", escapedSegments) + "/");
    }

    private static AuthenticationHeaderValue CreateBasicAuth(WebDavSettings settings)
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{settings.ApplicationPassword}"));
        return new AuthenticationHeaderValue("Basic", token);
    }
}
