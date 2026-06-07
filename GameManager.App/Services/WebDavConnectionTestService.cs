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
        try
        {
            using var response = await SendWebDavRequestAsync(client, "PROPFIND", BuildRemoteUri(settings), settings);
            if (IsSuccess(response.StatusCode))
            {
                return new WebDavConnectionTestResult(true, "连接成功");
            }

            if (response.StatusCode == HttpStatusCode.NotFound && HasRemoteDirectory(settings))
            {
                return await TryCreateRemoteDirectoriesAsync(client, settings);
            }

            return CreateFailureResult(response.StatusCode);
        }
        catch (Exception ex)
        {
            return new WebDavConnectionTestResult(false, $"连接失败：{ex.Message}");
        }
    }

    private static async Task<WebDavConnectionTestResult> TryCreateRemoteDirectoriesAsync(
        HttpClient client,
        WebDavSettings settings)
    {
        var segments = settings.RemoteDirectory.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var count = 1; count <= segments.Length; count++)
        {
            using var response = await SendWebDavRequestAsync(
                client,
                "MKCOL",
                BuildServerUri(settings.ServerUrl, segments.Take(count)),
                settings);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new WebDavConnectionTestResult(false, "连接失败：账号或应用密码不正确。");
            }

            if (response.StatusCode != HttpStatusCode.Created &&
                response.StatusCode != HttpStatusCode.MethodNotAllowed &&
                !IsSuccess(response.StatusCode))
            {
                return new WebDavConnectionTestResult(false, $"远程目录不存在，且创建失败：服务器返回 {(int)response.StatusCode}。");
            }
        }

        return new WebDavConnectionTestResult(true, "连接成功，已创建远程目录");
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
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => new WebDavConnectionTestResult(false, "连接失败：账号或应用密码不正确。"),
            HttpStatusCode.NotFound => new WebDavConnectionTestResult(false, "连接失败：远程目录不存在。"),
            _ => new WebDavConnectionTestResult(false, $"连接失败：服务器返回 {(int)statusCode}。")
        };
    }

    private static bool IsSuccess(HttpStatusCode statusCode) =>
        (int)statusCode is >= 200 and <= 299 || statusCode == (HttpStatusCode)207;

    private static bool HasRemoteDirectory(WebDavSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.RemoteDirectory.Trim().Trim('/'));

    private static Uri BuildRemoteUri(WebDavSettings settings) =>
        BuildServerUri(
            settings.ServerUrl,
            settings.RemoteDirectory.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries));

    private static Uri BuildServerUri(string serverUrl, IEnumerable<string> segments)
    {
        var baseUri = serverUrl.EndsWith("/", StringComparison.Ordinal) ? serverUrl : serverUrl + "/";
        return new Uri(baseUri + string.Join("/", segments.Select(Uri.EscapeDataString)) + "/");
    }

    private static AuthenticationHeaderValue CreateBasicAuth(WebDavSettings settings)
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{settings.ApplicationPassword}"));
        return new AuthenticationHeaderValue("Basic", token);
    }
}
