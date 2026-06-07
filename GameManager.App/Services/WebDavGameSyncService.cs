using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class WebDavGameSyncService : IWebDavGameSyncService
{
    private readonly Func<HttpClient> createClient;
    private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public WebDavGameSyncService()
        : this(() => new HttpClient())
    {
    }

    public WebDavGameSyncService(Func<HttpClient> createClient)
    {
        this.createClient = createClient;
    }

    public async Task<WebDavGameSyncResult> UploadGamesIndexAsync(WebDavSettings settings, IReadOnlyList<Game> games)
    {
        using var client = createClient();
        try
        {
            await EnsureDirectoriesAsync(client, settings, ["v2"]);
            var ids = games.Select(game => game.Id).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();
            await PutJsonAsync(client, settings, ["v2", "games-index.json"], ids);
            return new WebDavGameSyncResult(true, $"V2 游戏索引已上传：{ids.Length} 个");
        }
        catch (Exception ex)
        {
            return new WebDavGameSyncResult(false, $"V2 游戏索引上传失败：{ex.Message}");
        }
    }

    public async Task<WebDavGameSyncResult> UploadGameAsync(
        WebDavSettings settings,
        Game game,
        IReadOnlyList<PlaySession> sessions,
        string machineId,
        SaveManifest? saveManifest,
        string? latestBackupPath)
    {
        using var client = createClient();
        try
        {
            var gameRoot = new[] { "v2", "games", game.Id };
            await EnsureDirectoriesAsync(client, settings, gameRoot);
            await EnsureDirectoriesAsync(client, settings, [.. gameRoot, "paths"]);
            await EnsureDirectoriesAsync(client, settings, [.. gameRoot, "play-sessions"]);
            await EnsureDirectoriesAsync(client, settings, [.. gameRoot, "saves"]);
            var metadata = GameCloudMetadata.FromGame(game);
            var existingMetadata = await GetJsonAsync<GameCloudMetadata>(client, settings, [.. gameRoot, "metadata.json"]);
            metadata.PlaySessionIds = sessions
                .Select(session => session.Id)
                .Concat(existingMetadata?.PlaySessionIds ?? [])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (existingMetadata is not null)
            {
                metadata.TotalPlaySeconds = Math.Max(metadata.TotalPlaySeconds, existingMetadata.TotalPlaySeconds);
                metadata.LastLaunchTime = Max(metadata.LastLaunchTime, existingMetadata.LastLaunchTime);
                if (existingMetadata.UpdatedAtUtc.ToUniversalTime() >= metadata.UpdatedAtUtc.ToUniversalTime())
                {
                    metadata.Name = existingMetadata.Name;
                    metadata.CoverFileName = existingMetadata.CoverFileName;
                    metadata.UpdatedAtUtc = existingMetadata.UpdatedAtUtc.ToUniversalTime();
                }
            }
            var localGlobalMetadataWins = existingMetadata is null ||
                game.UpdatedAtUtc.ToUniversalTime() > existingMetadata.UpdatedAtUtc.ToUniversalTime();

            await PutJsonAsync(client, settings, [.. gameRoot, "metadata.json"], metadata);
            await PutJsonAsync(client, settings, [.. gameRoot, "paths", $"{machineId}.json"], new MachineGamePath
            {
                MachineId = machineId,
                ExecutablePath = game.ExecutablePath,
                GameRootPath = game.GameRootPath,
                SavePath = game.SavePath,
                LaunchArguments = game.LaunchArguments,
                WorkingDirectory = game.WorkingDirectory,
                MonitorProcessName = game.MonitorProcessName,
                RunAsAdministrator = game.RunAsAdministrator,
                SyncEnabled = game.SyncEnabled
            });

            foreach (var session in sessions)
            {
                await PutJsonAsync(client, settings, [.. gameRoot, "play-sessions", $"{session.Id}.json"], session);
            }

            if (localGlobalMetadataWins &&
                !string.IsNullOrWhiteSpace(game.CoverImagePath) &&
                File.Exists(game.CoverImagePath))
            {
                await EnsureDirectoriesAsync(client, settings, [.. gameRoot, "cover"]);
                await PutFileAsync(client, settings, [.. gameRoot, "cover", Path.GetFileName(game.CoverImagePath)], game.CoverImagePath);
            }

            if (!string.IsNullOrWhiteSpace(latestBackupPath) && File.Exists(latestBackupPath))
            {
                await PutFileAsync(client, settings, [.. gameRoot, "saves", "latest.zip"], latestBackupPath);
                if (saveManifest is not null)
                {
                    await PutJsonAsync(client, settings, [.. gameRoot, "saves", "save-manifest.json"], saveManifest);
                }
            }

            var existingIndex = await GetJsonAsync<string[]>(client, settings, ["v2", "games-index.json"]) ?? [];
            var updatedIndex = existingIndex
                .Append(game.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            await PutJsonAsync(client, settings, ["v2", "games-index.json"], updatedIndex);

            return new WebDavGameSyncResult(true, $"{game.Name} 的 V2 数据已上传");
        }
        catch (Exception ex)
        {
            return new WebDavGameSyncResult(false, $"{game.Name} 的 V2 数据上传失败：{ex.Message}");
        }
    }

    public async Task<IReadOnlyList<GameCloudMetadata>> DownloadGameMetadataAsync(WebDavSettings settings)
    {
        using var client = createClient();
        var ids = await GetJsonAsync<string[]>(client, settings, ["v2", "games-index.json"]) ?? [];
        var results = new List<GameCloudMetadata>();
        foreach (var id in ids.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            var metadata = await GetJsonAsync<GameCloudMetadata>(client, settings, ["v2", "games", id, "metadata.json"]);
            if (metadata is not null)
            {
                results.Add(metadata);
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<PlaySession>> DownloadPlaySessionsAsync(WebDavSettings settings, GameCloudMetadata metadata)
    {
        using var client = createClient();
        var sessions = new List<PlaySession>();
        foreach (var sessionId in metadata.PlaySessionIds)
        {
            var session = await GetJsonAsync<PlaySession>(
                client,
                settings,
                ["v2", "games", metadata.Id, "play-sessions", $"{sessionId}.json"]);
            if (session is not null)
            {
                sessions.Add(session);
            }
        }

        return sessions;
    }

    public async Task<MachineGamePath?> DownloadMachinePathAsync(
        WebDavSettings settings,
        string gameId,
        string machineId)
    {
        using var client = createClient();
        return await GetJsonAsync<MachineGamePath>(
            client,
            settings,
            ["v2", "games", gameId, "paths", $"{machineId}.json"]);
    }

    public async Task<string?> DownloadCoverAsync(WebDavSettings settings, GameCloudMetadata metadata, string coverDirectory)
    {
        if (string.IsNullOrWhiteSpace(metadata.CoverFileName))
        {
            return null;
        }

        var remoteFileName = Path.GetFileName(metadata.CoverFileName);
        if (string.IsNullOrWhiteSpace(remoteFileName))
        {
            return null;
        }

        using var client = createClient();
        using var request = CreateRequest(
            HttpMethod.Get,
            BuildUri(settings, ["v2", "games", metadata.Id, "cover", remoteFileName], false),
            settings);
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var gameCoverDirectory = Path.Combine(coverDirectory, SafePathSegment.Create(metadata.Id, "game"));
        Directory.CreateDirectory(gameCoverDirectory);
        var destination = Path.Combine(gameCoverDirectory, SafePathSegment.Create(remoteFileName, "cover"));
        return await WriteResponseAtomicallyAsync(response, destination) ? destination : null;
    }

    public async Task<SaveManifest?> DownloadSaveManifestAsync(WebDavSettings settings, string gameId)
    {
        using var client = createClient();
        return await GetJsonAsync<SaveManifest>(client, settings, ["v2", "games", gameId, "saves", "save-manifest.json"]);
    }

    public async Task<bool> DownloadLatestSaveAsync(WebDavSettings settings, string gameId, string destinationPath)
    {
        using var client = createClient();
        using var request = CreateRequest(HttpMethod.Get, BuildUri(settings, ["v2", "games", gameId, "saves", "latest.zip"], false), settings);
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        return await WriteResponseAtomicallyAsync(response, destinationPath);
    }

    private async Task EnsureDirectoriesAsync(HttpClient client, WebDavSettings settings, IReadOnlyList<string> segments)
    {
        var allSegments = settings.RemoteDirectory.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Concat(segments)
            .ToArray();
        for (var count = 1; count <= allSegments.Length; count++)
        {
            var current = allSegments.Take(count).ToArray();
            using var request = CreateRequest(new HttpMethod("MKCOL"), BuildServerUri(settings, current, true), settings);
            using var response = await client.SendAsync(request);
            if (!IsDirectoryReady(response.StatusCode))
            {
                throw new InvalidOperationException($"创建远程目录失败：{(int)response.StatusCode}");
            }
        }
    }

    private async Task PutJsonAsync<T>(HttpClient client, WebDavSettings settings, IReadOnlyList<string> segments, T value)
    {
        var json = JsonSerializer.Serialize(value, jsonOptions);
        using var request = CreateRequest(HttpMethod.Put, BuildUri(settings, segments, false), settings);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task PutFileAsync(HttpClient client, WebDavSettings settings, IReadOnlyList<string> segments, string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var request = CreateRequest(HttpMethod.Put, BuildUri(settings, segments, false), settings);
        request.Content = new StreamContent(stream);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task<T?> GetJsonAsync<T>(HttpClient client, WebDavSettings settings, IReadOnlyList<string> segments)
    {
        using var request = CreateRequest(HttpMethod.Get, BuildUri(settings, segments, false), settings);
        using var response = await client.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(stream, jsonOptions);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, WebDavSettings settings)
    {
        var request = new HttpRequestMessage(method, uri);
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{settings.ApplicationPassword}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        return request;
    }

    private static Uri BuildUri(WebDavSettings settings, IReadOnlyList<string> segments, bool directory)
    {
        var baseUri = settings.ServerUrl.EndsWith("/", StringComparison.Ordinal) ? settings.ServerUrl : settings.ServerUrl + "/";
        var path = string.Join("/", settings.RemoteDirectory.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Concat(segments)
            .Select(Uri.EscapeDataString));
        return new Uri(baseUri + path + (directory ? "/" : string.Empty));
    }

    private static Uri BuildServerUri(WebDavSettings settings, IReadOnlyList<string> segments, bool directory)
    {
        var baseUri = settings.ServerUrl.EndsWith("/", StringComparison.Ordinal) ? settings.ServerUrl : settings.ServerUrl + "/";
        var path = string.Join("/", segments.Select(Uri.EscapeDataString));
        return new Uri(baseUri + path + (directory ? "/" : string.Empty));
    }

    private static async Task<bool> WriteResponseAtomicallyAsync(HttpResponseMessage response, string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
        var temporaryPath = destinationPath + $".tmp-{Guid.NewGuid():N}";
        try
        {
            await using (var remote = await response.Content.ReadAsStreamAsync())
            await using (var local = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await remote.CopyToAsync(local);
            }

            File.Move(temporaryPath, destinationPath, true);
            return true;
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

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
            // Temporary cleanup must not hide the download result.
        }
    }

    private static bool IsDirectoryReady(HttpStatusCode statusCode)
    {
        return responseIsSuccess(statusCode) || statusCode == HttpStatusCode.MethodNotAllowed;
    }

    private static bool responseIsSuccess(HttpStatusCode statusCode)
    {
        return (int)statusCode is >= 200 and <= 299 || statusCode == (HttpStatusCode)207;
    }

    private static DateTime? Max(DateTime? first, DateTime? second)
    {
        if (first is null)
        {
            return second;
        }

        if (second is null)
        {
            return first;
        }

        return first >= second ? first : second;
    }
}
