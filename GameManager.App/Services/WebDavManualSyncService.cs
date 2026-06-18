using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using GameManager.App.Models;
using Microsoft.Data.Sqlite;

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
        try
        {
            await EnsureDirectoriesAsync(client, settings, ["metadata"]);
            var uploaded = await PutSanitizedUserDatabaseAsync(client, settings, databasePath);
            return uploaded
                ? new WebDavUploadResult(true, "用户信息上传完成：1 个文件", 1, 0)
                : new WebDavUploadResult(false, "用户信息上传失败", 0, 1);
        }
        catch (Exception ex)
        {
            return new WebDavUploadResult(false, $"用户信息上传失败：{ex.Message}", 0, 1);
        }
    }

    public async Task<WebDavUploadResult> UploadSaveBackupsAsync(WebDavSettings settings, string saveBackupsDirectory)
    {
        if (!Directory.Exists(saveBackupsDirectory))
        {
            return new WebDavUploadResult(true, "没有需要上传的存档备份", 0, 0);
        }

        var backupFiles = GetLatestBackupPerGameDirectory(saveBackupsDirectory);
        if (backupFiles.Count == 0)
        {
            return new WebDavUploadResult(true, "没有需要上传的存档备份", 0, 0);
        }

        using var client = createClient();
        var uploaded = 0;
        var failed = 0;
        foreach (var backupFile in backupFiles)
        {
            try
            {
                var relativeDirectory = Path.GetRelativePath(
                    saveBackupsDirectory,
                    Path.GetDirectoryName(backupFile) ?? saveBackupsDirectory);
                var relativeSegments = relativeDirectory == "."
                    ? Array.Empty<string>()
                    : relativeDirectory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Where(segment => !string.IsNullOrWhiteSpace(segment))
                        .ToArray();
                var directorySegments = new[] { "save-backups" }.Concat(relativeSegments).ToArray();
                await EnsureDirectoriesAsync(client, settings, directorySegments);
                var fileSegments = directorySegments.Append(Path.GetFileName(backupFile)).ToArray();
                if (await PutFileAsync(client, settings, backupFile, fileSegments))
                {
                    uploaded++;
                }
                else
                {
                    failed++;
                }
            }
            catch
            {
                failed++;
            }
        }

        return new WebDavUploadResult(
            failed == 0,
            failed == 0 ? $"存档备份上传完成：{uploaded} 个文件" : $"存档备份上传完成：成功 {uploaded} 个，失败 {failed} 个",
            uploaded,
            failed);
    }

    public async Task<WebDavDownloadResult> DownloadUserDataAsync(WebDavSettings settings, string databasePath)
    {
        using var client = createClient();
        try
        {
            using var request = CreateRequest(HttpMethod.Get, BuildRemoteUri(settings, ["metadata", "app.db"], false), settings);
            using var response = await client.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new WebDavDownloadResult(true, "云端暂无用户信息", 0, 0);
            }

            if (!IsSuccess(response.StatusCode))
            {
                return new WebDavDownloadResult(false, $"用户信息下载失败：服务器返回 {(int)response.StatusCode}", 0, 1);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? ".");
            var temporaryPath = databasePath + $".tmp-{Guid.NewGuid():N}";
            try
            {
                await WriteResponseToFileAsync(response, temporaryPath);
                if (File.Exists(databasePath))
                {
                    File.Copy(databasePath, databasePath + ".bak", true);
                }

                File.Move(temporaryPath, databasePath, true);
                RestoreLocalOnlyMetadata(databasePath + ".bak", databasePath);
            }
            finally
            {
                TryDeleteFile(temporaryPath);
            }

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

            var downloaded = 0;
            var failed = 0;
            foreach (var remoteZipPath in remoteZipPaths)
            {
                var localPath = BuildLocalBackupPath(saveBackupsDirectory, remoteZipPath);
                if (localPath is null)
                {
                    failed++;
                    continue;
                }

                var segments = new[] { "save-backups" }
                    .Concat(remoteZipPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
                    .ToArray();
                if (await DownloadFileAsync(client, settings, segments, localPath))
                {
                    downloaded++;
                }
                else
                {
                    failed++;
                }
            }

            return new WebDavDownloadResult(
                failed == 0,
                failed == 0 ? $"存档备份下载完成：{downloaded} 个文件" : $"存档备份下载完成：成功 {downloaded} 个，失败 {failed} 个",
                downloaded,
                failed);
        }
        catch (Exception ex)
        {
            return new WebDavDownloadResult(false, $"存档备份下载失败：{ex.Message}", 0, 1);
        }
    }

    private static async Task<IReadOnlyList<string>> ListSaveBackupZipPathsAsync(HttpClient client, WebDavSettings settings)
    {
        var pending = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var zipPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Enqueue(string.Empty);

        while (pending.Count > 0)
        {
            var relativeDirectory = pending.Dequeue();
            if (!visited.Add(relativeDirectory))
            {
                continue;
            }

            var segments = new[] { "save-backups" }
                .Concat(relativeDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries))
                .ToArray();
            using var request = CreateRequest(new HttpMethod("PROPFIND"), BuildRemoteUri(settings, segments, true), settings);
            request.Headers.TryAddWithoutValidation("Depth", "1");
            using var response = await client.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.NotFound && string.IsNullOrEmpty(relativeDirectory))
            {
                return [];
            }

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
                    pending.Enqueue(entry.RelativePath);
                }
                else if (entry.RelativePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    zipPaths.Add(entry.RelativePath);
                }
            }
        }

        return zipPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<WebDavRemoteEntry> ParseSaveBackupEntries(WebDavSettings settings, string xml)
    {
        var document = XDocument.Parse(xml);
        return document.Descendants()
            .Where(element => element.Name.LocalName.Equals("response", StringComparison.OrdinalIgnoreCase))
            .Select(response =>
            {
                var href = response.Descendants()
                    .FirstOrDefault(element => element.Name.LocalName.Equals("href", StringComparison.OrdinalIgnoreCase))
                    ?.Value;
                var collection = response.Descendants()
                    .Any(element => element.Name.LocalName.Equals("collection", StringComparison.OrdinalIgnoreCase));
                return TryGetSaveBackupEntry(settings, href, collection);
            })
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .DistinctBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static WebDavRemoteEntry? TryGetSaveBackupEntry(WebDavSettings settings, string? href, bool collection)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var path = Uri.TryCreate(href.Trim(), UriKind.Absolute, out var absolute)
            ? absolute.AbsolutePath
            : href.Trim();
        path = Uri.UnescapeDataString(path).Replace('\\', '/');
        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            path = "/" + path;
        }

        var prefix = "/" + string.Join("/", settings.RemoteDirectory.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Concat(["save-backups"])) + "/";
        var index = path.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var relative = path[(index + prefix.Length)..].Trim('/');
        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(IsUnsafePathSegment))
        {
            return null;
        }

        return new WebDavRemoteEntry(string.Join("/", segments), collection || path.EndsWith("/", StringComparison.Ordinal));
    }

    private static async Task<bool> PutSanitizedUserDatabaseAsync(
        HttpClient client,
        WebDavSettings settings,
        string databasePath)
    {
        var uploadPath = CreateSanitizedUserDatabaseCopy(databasePath) ?? databasePath;
        try
        {
            return await PutFileAsync(client, settings, uploadPath, ["metadata", "app.db"]);
        }
        finally
        {
            if (!string.Equals(uploadPath, databasePath, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(uploadPath);
            }
        }
    }

    private static string? CreateSanitizedUserDatabaseCopy(string databasePath)
    {
        if (!IsSqliteDatabase(databasePath))
        {
            return null;
        }

        var temporaryPath = Path.Combine(Path.GetTempPath(), $"FireflyGameManager-webdav-{Guid.NewGuid():N}.db");
        try
        {
            using var source = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Pooling = false
            }.ToString());
            using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = temporaryPath,
                Pooling = false
            }.ToString());
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);

            foreach (var tableName in new[] { "bangumi_collection_states", "game_external_metadata", "external_metadata_conflicts" })
            {
                using var check = destination.CreateCommand();
                check.CommandText =
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
                check.Parameters.AddWithValue("$tableName", tableName);
                if ((long)check.ExecuteScalar()! > 0)
                {
                    using var clear = destination.CreateCommand();
                    clear.CommandText = $"DELETE FROM {tableName};";
                    clear.ExecuteNonQuery();
                }
            }

            return temporaryPath;
        }
        catch
        {
            TryDeleteFile(temporaryPath);
            throw new InvalidOperationException("无法创建不含 Bangumi 收藏缓存的安全上传副本。");
        }
    }

    private static bool IsSqliteDatabase(string path)
    {
        try
        {
            Span<byte> header = stackalloc byte[16];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return stream.Read(header) == header.Length &&
                header.SequenceEqual("SQLite format 3\0"u8);
        }
        catch
        {
            return false;
        }
    }

    private static void RestoreLocalOnlyMetadata(string backupPath, string databasePath)
    {
        if (!File.Exists(backupPath))
        {
            return;
        }

        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Pooling = false
            }.ToString());
            connection.Open();
            using (var create = connection.CreateCommand())
            {
                create.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS bangumi_collection_states (
                        game_id TEXT PRIMARY KEY,
                        subject_id TEXT NOT NULL,
                        username TEXT NOT NULL,
                        collection_type TEXT NOT NULL DEFAULT 'None',
                        rating INTEGER NOT NULL DEFAULT 0,
                        comment TEXT NOT NULL DEFAULT '',
                        remote_updated_at TEXT,
                        last_synced_at TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS game_external_metadata (
                        game_id TEXT PRIMARY KEY,
                        provider TEXT NOT NULL,
                        subject_id TEXT NOT NULL,
                        is_linked INTEGER NOT NULL DEFAULT 1,
                        original_name TEXT NOT NULL DEFAULT '',
                        localized_name TEXT NOT NULL DEFAULT '',
                        summary TEXT NOT NULL DEFAULT '',
                        release_date TEXT NOT NULL DEFAULT '',
                        developer TEXT NOT NULL DEFAULT '',
                        publisher TEXT NOT NULL DEFAULT '',
                        tags_json TEXT NOT NULL DEFAULT '[]',
                        image_url TEXT NOT NULL DEFAULT '',
                        subject_url TEXT NOT NULL DEFAULT '',
                        source_updated_at TEXT NOT NULL,
                        imported_at TEXT NOT NULL,
                        updated_at TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS external_metadata_conflicts (
                        game_id TEXT PRIMARY KEY,
                        local_snapshot_json TEXT NOT NULL,
                        cloud_snapshot_json TEXT NOT NULL,
                        detected_at TEXT NOT NULL,
                        reason TEXT NOT NULL DEFAULT ''
                    );
                    """;
                create.ExecuteNonQuery();
            }

            using (var attach = connection.CreateCommand())
            {
                attach.CommandText = "ATTACH DATABASE $backupPath AS local_backup;";
                attach.Parameters.AddWithValue("$backupPath", backupPath);
                attach.ExecuteNonQuery();
            }

            foreach (var tableName in new[] { "bangumi_collection_states", "game_external_metadata", "external_metadata_conflicts" })
            {
                using var check = connection.CreateCommand();
                check.CommandText =
                    "SELECT COUNT(*) FROM local_backup.sqlite_master WHERE type = 'table' AND name = $tableName;";
                check.Parameters.AddWithValue("$tableName", tableName);
                if ((long)check.ExecuteScalar()! > 0)
                {
                    using var restore = connection.CreateCommand();
                    restore.CommandText = $"INSERT OR REPLACE INTO {tableName} SELECT * FROM local_backup.{tableName};";
                    restore.ExecuteNonQuery();
                }
            }
        }
        catch
        {
            // A legacy or invalid database should still be downloaded successfully.
        }
    }

    private static async Task EnsureDirectoriesAsync(HttpClient client, WebDavSettings settings, IReadOnlyList<string> requestedSegments)
    {
        var allSegments = settings.RemoteDirectory.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Concat(requestedSegments)
            .ToArray();
        for (var count = 1; count <= allSegments.Length; count++)
        {
            using var request = CreateRequest(new HttpMethod("MKCOL"), BuildServerUri(settings, allSegments.Take(count), true), settings);
            using var response = await client.SendAsync(request);
            if (!IsDirectoryReady(response.StatusCode))
            {
                throw new InvalidOperationException($"创建远程目录失败：服务器返回 {(int)response.StatusCode}");
            }
        }
    }

    private static async Task<bool> PutFileAsync(HttpClient client, WebDavSettings settings, string localPath, IReadOnlyList<string> segments)
    {
        await using var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var request = CreateRequest(HttpMethod.Put, BuildRemoteUri(settings, segments, false), settings);
        request.Content = new StreamContent(stream);
        using var response = await client.SendAsync(request);
        return IsSuccess(response.StatusCode);
    }

    private static async Task<bool> DownloadFileAsync(HttpClient client, WebDavSettings settings, IReadOnlyList<string> segments, string localPath)
    {
        using var request = CreateRequest(HttpMethod.Get, BuildRemoteUri(settings, segments, false), settings);
        using var response = await client.SendAsync(request);
        if (!IsSuccess(response.StatusCode))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(localPath) ?? ".");
        var temporaryPath = localPath + $".tmp-{Guid.NewGuid():N}";
        try
        {
            await WriteResponseToFileAsync(response, temporaryPath);
            File.Move(temporaryPath, localPath, true);
            return true;
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    private static async Task WriteResponseToFileAsync(HttpResponseMessage response, string path)
    {
        await using var remote = await response.Content.ReadAsStreamAsync();
        await using var local = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await remote.CopyToAsync(local);
    }

    private static string? BuildLocalBackupPath(string rootDirectory, string relativePath)
    {
        var root = Path.GetFullPath(rootDirectory);
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(IsUnsafePathSegment))
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine([root, .. segments]));
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    private static bool IsUnsafePathSegment(string segment) =>
        string.IsNullOrWhiteSpace(segment) ||
        segment is "." or ".." ||
        segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
        !string.Equals(SafePathSegment.Create(segment), segment, StringComparison.Ordinal);

    private static List<string> GetLatestBackupPerGameDirectory(string root) =>
        Directory.EnumerateFiles(root, "*.zip", SearchOption.AllDirectories)
            .GroupBy(path => Path.GetDirectoryName(path) ?? root, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(File.GetLastWriteTime).ThenByDescending(path => path, StringComparer.OrdinalIgnoreCase).First())
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, WebDavSettings settings)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = CreateBasicAuth(settings);
        return request;
    }

    private static Uri BuildRemoteUri(WebDavSettings settings, IReadOnlyList<string> segments, bool directory) =>
        BuildServerUri(settings, settings.RemoteDirectory.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Concat(segments), directory);

    private static Uri BuildServerUri(WebDavSettings settings, IEnumerable<string> segments, bool directory)
    {
        var baseUri = settings.ServerUrl.EndsWith("/", StringComparison.Ordinal) ? settings.ServerUrl : settings.ServerUrl + "/";
        var path = string.Join("/", segments.Select(Uri.EscapeDataString));
        return new Uri(baseUri + path + (directory ? "/" : string.Empty));
    }

    private static AuthenticationHeaderValue CreateBasicAuth(WebDavSettings settings)
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{settings.ApplicationPassword}"));
        return new AuthenticationHeaderValue("Basic", token);
    }

    private static bool IsSuccess(HttpStatusCode statusCode) =>
        (int)statusCode is >= 200 and <= 299 || statusCode == (HttpStatusCode)207;

    private static bool IsDirectoryReady(HttpStatusCode statusCode) =>
        IsSuccess(statusCode) || statusCode == HttpStatusCode.MethodNotAllowed;

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary cleanup must not hide the synchronization result.
        }
    }

    private sealed record WebDavRemoteEntry(string RelativePath, bool IsDirectory);
}
