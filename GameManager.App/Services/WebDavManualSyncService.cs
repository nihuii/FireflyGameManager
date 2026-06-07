using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class WebDavManualSyncService : IWebDavManualSyncService
{
    private readonly Func<HttpClient> createClient;

    public WebDavManualSyncService()
        : this(() => new HttpClient())
    {
    }

    public WebDavManualSyncService(Func<HttpClient> createClient)
    {
        this.createClient = createClient;
    }

    public async Task<WebDavUploadResult> UploadUserDataAsync(WebDavSettings settings, string databasePath)
    {
        if (!File.Exists(databasePath))
        {
            return new WebDavUploadResult(false, "用户信息数据库不存在", 0, 1);
        }

        using var client = createClient();
        await EnsureDirectoryAsync(client, settings, []);
        await EnsureDirectoryAsync(client, settings, ["metadata"]);

        var uploaded = await PutFileAsync(client, settings, databasePath, ["metadata", "app.db"]);
        return uploaded
            ? new WebDavUploadResult(true, "用户信息上传完成：1 个文件", 1, 0)
            : new WebDavUploadResult(false, "用户信息上传失败", 0, 1);
    }

    public async Task<WebDavUploadResult> UploadSaveBackupsAsync(WebDavSettings settings, string saveBackupsDirectory)
    {
        if (!Directory.Exists(saveBackupsDirectory))
        {
            return new WebDavUploadResult(false, "本地存档备份目录不存在", 0, 1);
        }

        var backupFiles = GetLatestBackupPerGameDirectory(saveBackupsDirectory);

        if (backupFiles.Count == 0)
        {
            return new WebDavUploadResult(true, "没有需要上传的存档备份", 0, 0);
        }

        using var client = createClient();
        await EnsureDirectoryAsync(client, settings, []);
        await EnsureDirectoryAsync(client, settings, ["save-backups"]);

        var uploadedCount = 0;
        var failedCount = 0;
        var ensuredDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var backupFile in backupFiles)
        {
            var relativeDirectory = Path.GetRelativePath(saveBackupsDirectory, Path.GetDirectoryName(backupFile) ?? saveBackupsDirectory);
            var relativeSegments = relativeDirectory == "."
                ? Array.Empty<string>()
                : relativeDirectory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Where(segment => !string.IsNullOrWhiteSpace(segment))
                    .ToArray();
            var remoteDirectorySegments = new[] { "save-backups" }.Concat(relativeSegments).ToArray();
            var remoteDirectoryKey = string.Join("/", remoteDirectorySegments);
            if (ensuredDirectories.Add(remoteDirectoryKey))
            {
                await EnsureDirectoryAsync(client, settings, remoteDirectorySegments);
            }

            var remoteFileSegments = remoteDirectorySegments.Concat([Path.GetFileName(backupFile)]).ToArray();
            if (await PutFileAsync(client, settings, backupFile, remoteFileSegments))
            {
                uploadedCount++;
            }
            else
            {
                failedCount++;
            }
        }

        var success = failedCount == 0;
        var message = success
            ? $"存档备份上传完成：{uploadedCount} 个文件"
            : $"存档备份上传完成：成功 {uploadedCount} 个，失败 {failedCount} 个";
        return new WebDavUploadResult(success, message, uploadedCount, failedCount);
    }

    public async Task<WebDavDownloadResult> DownloadUserDataAsync(WebDavSettings settings, string databasePath)
    {
        using var client = createClient();

        try
        {
            using var request = CreateRequest("GET", BuildRemoteUri(settings, ["metadata", "app.db"], false), settings);
            using var response = await client.SendAsync(request);
            if (!IsSuccess(response.StatusCode))
            {
                return new WebDavDownloadResult(false, $"用户信息下载失败：服务器返回 {(int)response.StatusCode}", 0, 1);
            }

            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(databasePath))
            {
                File.Copy(databasePath, databasePath + ".bak", true);
            }

            await using var remoteStream = await response.Content.ReadAsStreamAsync();
            await using var localStream = new FileStream(databasePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await remoteStream.CopyToAsync(localStream);

            return new WebDavDownloadResult(true, "用户信息下载完成：1 个文件，本地旧数据库已备份为 .bak", 1, 0);
        }
        catch (Exception ex)
        {
            return new WebDavDownloadResult(false, $"用户信息下载失败：{ex.Message}", 0, 1);
        }
    }

    public async Task<WebDavDownloadResult> DownloadSaveBackupsAsync(WebDavSettings settings, string saveBackupsDirectory)
    {
        using var client = createClient();

        try
        {
            var remoteZipPaths = await ListSaveBackupZipPathsAsync(client, settings);
            if (remoteZipPaths.Count == 0)
            {
                return new WebDavDownloadResult(true, "没有可下载的存档备份", 0, 0);
            }

            Directory.CreateDirectory(saveBackupsDirectory);

            var downloadedCount = 0;
            var failedCount = 0;
            foreach (var remoteZipPath in remoteZipPaths)
            {
                var localPath = BuildLocalBackupPath(saveBackupsDirectory, remoteZipPath);
                if (localPath is null)
                {
                    failedCount++;
                    continue;
                }

                var remoteSegments = new[] { "save-backups" }
                    .Concat(remoteZipPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
                    .ToArray();
                if (await DownloadFileAsync(client, settings, remoteSegments, localPath))
                {
                    downloadedCount++;
                }
                else
                {
                    failedCount++;
                }
            }

            var success = failedCount == 0;
            var message = success
                ? $"存档备份下载完成：{downloadedCount} 个文件"
                : $"存档备份下载完成：成功 {downloadedCount} 个，失败 {failedCount} 个";
            return new WebDavDownloadResult(success, message, downloadedCount, failedCount);
        }
        catch (Exception ex)
        {
            return new WebDavDownloadResult(false, $"存档备份下载失败：{ex.Message}", 0, 1);
        }
    }

    private static async Task<IReadOnlyList<string>> ListSaveBackupZipPathsAsync(HttpClient client, WebDavSettings settings)
    {
        var pendingDirectories = new Queue<string>();
        var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var zipPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        pendingDirectories.Enqueue(string.Empty);
        while (pendingDirectories.Count > 0)
        {
            var relativeDirectory = pendingDirectories.Dequeue();
            if (!visitedDirectories.Add(relativeDirectory))
            {
                continue;
            }

            var remoteSegments = new[] { "save-backups" }
                .Concat(SplitRemotePath(relativeDirectory))
                .ToArray();
            using var request = CreateRequest("PROPFIND", BuildRemoteUri(settings, remoteSegments, true), settings);
            request.Headers.TryAddWithoutValidation("Depth", "1");
            using var response = await client.SendAsync(request);
            if (!IsSuccess(response.StatusCode))
            {
                if (string.IsNullOrEmpty(relativeDirectory))
                {
                    throw new InvalidOperationException($"服务器返回 {(int)response.StatusCode}");
                }

                continue;
            }

            var xml = await response.Content.ReadAsStringAsync();
            foreach (var entry in ParseSaveBackupEntries(settings, xml))
            {
                if (entry.RelativePath.Equals(relativeDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entry.IsDirectory)
                {
                    pendingDirectories.Enqueue(entry.RelativePath);
                }
                else if (entry.RelativePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    zipPaths.Add(entry.RelativePath);
                }
            }
        }

        return zipPaths
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task EnsureDirectoryAsync(HttpClient client, WebDavSettings settings, IReadOnlyList<string> remoteSegments)
    {
        using var request = CreateRequest("MKCOL", BuildRemoteUri(settings, remoteSegments, true), settings);
        using var response = await client.SendAsync(request);
        if (IsDirectoryReady(response.StatusCode))
        {
            return;
        }

        throw new InvalidOperationException($"创建远程目录失败：服务器返回 {(int)response.StatusCode}。");
    }

    private static async Task<bool> PutFileAsync(
        HttpClient client,
        WebDavSettings settings,
        string localPath,
        IReadOnlyList<string> remoteSegments)
    {
        using var fileStream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var request = CreateRequest("PUT", BuildRemoteUri(settings, remoteSegments, false), settings);
        request.Content = new StreamContent(fileStream);
        using var response = await client.SendAsync(request);
        return IsSuccess(response.StatusCode);
    }

    private static async Task<bool> DownloadFileAsync(
        HttpClient client,
        WebDavSettings settings,
        IReadOnlyList<string> remoteSegments,
        string localPath)
    {
        using var request = CreateRequest("GET", BuildRemoteUri(settings, remoteSegments, false), settings);
        using var response = await client.SendAsync(request);
        if (!IsSuccess(response.StatusCode))
        {
            return false;
        }

        var localDirectory = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrWhiteSpace(localDirectory))
        {
            Directory.CreateDirectory(localDirectory);
        }

        await using var remoteStream = await response.Content.ReadAsStreamAsync();
        await using var localStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await remoteStream.CopyToAsync(localStream);
        return true;
    }

    private static IReadOnlyList<WebDavRemoteEntry> ParseSaveBackupEntries(WebDavSettings settings, string xml)
    {
        var document = XDocument.Parse(xml);
        return document
            .Descendants()
            .Where(element => element.Name.LocalName.Equals("response", StringComparison.OrdinalIgnoreCase))
            .Select(response =>
            {
                var href = response
                    .Descendants()
                    .FirstOrDefault(element => element.Name.LocalName.Equals("href", StringComparison.OrdinalIgnoreCase))
                    ?.Value;
                var isCollection = response
                    .Descendants()
                    .Any(element => element.Name.LocalName.Equals("collection", StringComparison.OrdinalIgnoreCase));
                return TryGetSaveBackupEntry(settings, href, isCollection);
            })
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .DistinctBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static WebDavRemoteEntry? TryGetSaveBackupEntry(WebDavSettings settings, string? href, bool isCollection)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var path = href.Trim();
        if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
        {
            path = absoluteUri.AbsolutePath;
        }

        path = Uri.UnescapeDataString(path).Replace('\\', '/');
        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            path = "/" + path;
        }

        var isDirectory = isCollection || path.EndsWith("/", StringComparison.Ordinal);

        var remotePrefixSegments = settings.RemoteDirectory
            .Trim()
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Concat(["save-backups"]);
        var marker = "/" + string.Join("/", remotePrefixSegments) + "/";
        var markerIndex = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var relativePath = path[(markerIndex + marker.Length)..].Trim('/');
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(IsUnsafePathSegment))
        {
            return null;
        }

        return new WebDavRemoteEntry(string.Join("/", segments), isDirectory);
    }

    private static string[] SplitRemotePath(string remotePath)
    {
        return remotePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string? BuildLocalBackupPath(string saveBackupsDirectory, string remoteRelativePath)
    {
        var root = Path.GetFullPath(saveBackupsDirectory);
        var segments = remoteRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(IsUnsafePathSegment))
        {
            return null;
        }

        var localPath = Path.GetFullPath(Path.Combine([root, .. segments]));
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return localPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            ? localPath
            : null;
    }

    private static bool IsUnsafePathSegment(string segment)
    {
        return string.IsNullOrWhiteSpace(segment) ||
            segment == "." ||
            segment == ".." ||
            segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
    }

    private static List<string> GetLatestBackupPerGameDirectory(string saveBackupsDirectory)
    {
        return Directory
            .EnumerateFiles(saveBackupsDirectory, "*.zip", SearchOption.AllDirectories)
            .GroupBy(path => Path.GetDirectoryName(path) ?? saveBackupsDirectory, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(path => File.GetLastWriteTime(path))
                .ThenByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HttpRequestMessage CreateRequest(string method, Uri uri, WebDavSettings settings)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), uri);
        request.Headers.Authorization = CreateBasicAuth(settings);
        return request;
    }

    private static Uri BuildRemoteUri(WebDavSettings settings, IReadOnlyList<string> remoteSegments, bool isDirectory)
    {
        var baseUri = settings.ServerUrl.EndsWith("/", StringComparison.Ordinal)
            ? settings.ServerUrl
            : settings.ServerUrl + "/";
        var allSegments = settings.RemoteDirectory
            .Trim()
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Concat(remoteSegments)
            .Select(Uri.EscapeDataString)
            .ToList();
        var path = string.Join("/", allSegments);
        if (string.IsNullOrWhiteSpace(path))
        {
            return new Uri(baseUri);
        }

        return new Uri(baseUri + path + (isDirectory ? "/" : string.Empty));
    }

    private static AuthenticationHeaderValue CreateBasicAuth(WebDavSettings settings)
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{settings.ApplicationPassword}"));
        return new AuthenticationHeaderValue("Basic", token);
    }

    private static bool IsSuccess(HttpStatusCode statusCode)
    {
        return ((int)statusCode >= 200 && (int)statusCode <= 299) || statusCode == (HttpStatusCode)207;
    }

    private static bool IsDirectoryReady(HttpStatusCode statusCode)
    {
        return IsSuccess(statusCode) || statusCode == HttpStatusCode.MethodNotAllowed;
    }

    private sealed record WebDavRemoteEntry(string RelativePath, bool IsDirectory);
}
