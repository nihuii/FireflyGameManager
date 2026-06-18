using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class BangumiApiClient : IBangumiApiClient
{
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(15);
    private readonly Func<HttpClient> createClient;
    private readonly TimeSpan requestTimeout;

    public BangumiApiClient(Func<HttpClient>? createClient = null, TimeSpan? requestTimeout = null)
    {
        this.createClient = createClient ?? (() => new HttpClient());
        this.requestTimeout = requestTimeout ?? DefaultRequestTimeout;
    }

    public async Task<BangumiAccount> GetCurrentUserAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "/v0/me", accessToken);
        using var response = await SendAsync(request, cancellationToken);
        using var document = await ReadDocumentAsync(response, cancellationToken);
        var root = document.RootElement;
        var username = BangumiDtoMapper.GetString(root, "username");
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException("Bangumi 返回的账号信息无效。");
        }

        var avatarUrl = root.TryGetProperty("avatar", out var avatar)
            ? BangumiDtoMapper.GetString(avatar, "large")
            : string.Empty;
        return new BangumiAccount(
            username,
            BangumiDtoMapper.GetString(root, "nickname"),
            avatarUrl,
            accessToken,
            DateTime.UtcNow,
            RequiresReconnect: false);
    }

    public async Task<IReadOnlyList<GameMetadataSearchResult>> SearchGamesAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalized = query.Trim();
        var modernResults = await SearchModernGamesAsync(normalized, cancellationToken);
        IReadOnlyList<GameMetadataSearchResult> legacyResults = [];
        if (ContainsCjk(normalized) &&
            (modernResults.Count == 0 || !HasStrongTitleMatch(normalized, modernResults)))
        {
            legacyResults = await TrySearchLegacyGamesAsync(normalized, cancellationToken);
        }

        return RankAndMergeSearchResults(normalized, legacyResults.Concat(modernResults));
    }

    private async Task<IReadOnlyList<GameMetadataSearchResult>> SearchModernGamesAsync(
        string query,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "/v0/search/subjects?limit=20&offset=0");
        request.Content = JsonContent.Create(new
        {
            keyword = query,
            sort = "match",
            filter = new { type = new[] { 4 } }
        });
        using var response = await SendAsync(request, cancellationToken);
        using var document = await ReadDocumentAsync(response, cancellationToken);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return data.EnumerateArray()
            .Where(IsGameSubject)
            .Select(BangumiDtoMapper.ToSearchResult)
            .Where(result => !string.IsNullOrWhiteSpace(result.SubjectId))
            .ToList();
    }

    private async Task<IReadOnlyList<GameMetadataSearchResult>> TrySearchLegacyGamesAsync(
        string query,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SearchLegacyGamesAsync(query, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is BangumiApiException or JsonException or HttpRequestException or InvalidOperationException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<GameMetadataSearchResult>> SearchLegacyGamesAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var path = $"/search/subject/{Uri.EscapeDataString(query)}?type=4&responseGroup=large&max_results=20";
        using var request = CreateRequest(HttpMethod.Get, path);
        using var response = await SendAsync(request, cancellationToken);
        using var document = await ReadDocumentAsync(response, cancellationToken);
        if (!document.RootElement.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return list.EnumerateArray()
            .Where(IsGameSubject)
            .Select(BangumiDtoMapper.ToSearchResult)
            .Where(result => !string.IsNullOrWhiteSpace(result.SubjectId))
            .ToList();
    }

    public async Task<ExternalGameMetadata?> GetGameDetailsAsync(
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        return await GetGameDetailsCoreAsync(subjectId, null, cancellationToken);
    }

    public async Task<ExternalGameMetadata?> GetGameDetailsAsync(
        string subjectId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        return await GetGameDetailsCoreAsync(subjectId, accessToken, cancellationToken);
    }

    private async Task<ExternalGameMetadata?> GetGameDetailsCoreAsync(
        string subjectId,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = CreateRequest(
                HttpMethod.Get,
                $"/v0/subjects/{Uri.EscapeDataString(subjectId)}",
                accessToken);
            using var response = await SendAsync(request, cancellationToken, allowNotFound: true);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                if (attempt == 0)
                {
                    continue;
                }

                return null;
            }

            using var document = await ReadDocumentAsync(response, cancellationToken);
            return BangumiDtoMapper.ToMetadata(document.RootElement);
        }

        return null;
    }

    public async Task<BangumiCollectionState?> GetCollectionAsync(
        BangumiAccount account,
        string gameId,
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        var username = Uri.EscapeDataString(account.Username);
        using var request = CreateRequest(
            HttpMethod.Get,
            $"/v0/users/{username}/collections/{Uri.EscapeDataString(subjectId)}",
            account.AccessToken);
        using var response = await SendAsync(request, cancellationToken, allowNotFound: true);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        using var document = await ReadDocumentAsync(response, cancellationToken);
        var root = document.RootElement;
        return new BangumiCollectionState(
            gameId,
            subjectId,
            account.Username,
            ParseCollectionType(root),
            root.TryGetProperty("rate", out var rate) ? rate.GetInt32() : 0,
            BangumiDtoMapper.GetString(root, "comment"),
            ParseUtcDate(BangumiDtoMapper.GetString(root, "updated_at")),
            DateTime.UtcNow);
    }

    public async Task<BangumiCollectionState> SaveCollectionAsync(
        BangumiAccount account,
        BangumiCollectionState state,
        bool isExistingCollection = false,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            isExistingCollection ? HttpMethod.Patch : HttpMethod.Post,
            $"/v0/users/-/collections/{Uri.EscapeDataString(state.SubjectId)}",
            account.AccessToken);
        var payload = new Dictionary<string, object>
        {
            ["type"] = (int)state.Type
        };
        if (state.Rating > 0)
        {
            payload["rate"] = Math.Clamp(state.Rating, 1, 10);
        }

        if (!string.IsNullOrWhiteSpace(state.Comment))
        {
            payload["comment"] = state.Comment;
        }

        request.Content = JsonContent.Create(payload);
        using var response = await SendAsync(request, cancellationToken);
        return state with { Username = account.Username, LastSyncedAtUtc = DateTime.UtcNow };
    }

    private static IReadOnlyList<GameMetadataSearchResult> RankAndMergeSearchResults(
        string query,
        IEnumerable<GameMetadataSearchResult> results)
    {
        var indexedResults = results
            .Select((Result, Index) => new
            {
                Result,
                Index,
                Rank = MetadataMatchScorer.Score(query, Result),
                Completeness = GetCompletenessScore(Result)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Result.SubjectId))
            .GroupBy(item => item.Result.SubjectId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(item => item.Rank)
                .ThenByDescending(item => item.Completeness)
                .ThenBy(item => item.Index)
                .First())
            .OrderBy(item => item.Rank)
            .ThenByDescending(item => item.Completeness)
            .ThenBy(item => item.Index)
            .Select(item => item.Result)
            .ToList();

        return indexedResults;
    }

    private static bool HasStrongTitleMatch(string query, IEnumerable<GameMetadataSearchResult> results) =>
        results.Any(result => MetadataMatchScorer.Score(query, result) <= 1);

    private static int GetCompletenessScore(GameMetadataSearchResult result)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(result.ImageUrl))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(result.SummaryPreview))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(result.ReleaseDate))
        {
            score++;
        }

        return score;
    }

    private static bool IsGameSubject(JsonElement subject)
    {
        if (!subject.TryGetProperty("type", out var type))
        {
            return true;
        }

        return type.ValueKind switch
        {
            JsonValueKind.Number => type.GetInt32() == 4,
            JsonValueKind.String => type.GetString() == "4",
            _ => false
        };
    }

    private static bool ContainsCjk(string value) =>
        value.Any(character =>
            character is >= '\u3400' and <= '\u9fff' ||
            character is >= '\uf900' and <= '\ufaff' ||
            character is >= '\u3040' and <= '\u30ff' ||
            character is >= '\uac00' and <= '\ud7af');

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, string? accessToken = null)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri("https://api.bgm.tv"), relativeUrl));
        request.Headers.UserAgent.ParseAdd("FireflyGameManager/1.0");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        bool allowNotFound = false)
    {
        using var client = createClient();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(requestTimeout);
            HttpResponseMessage response;
            try
            {
                using var attemptRequest = await CloneRequestAsync(request, cancellationToken);
                response = await client.SendAsync(attemptRequest, timeout.Token);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new BangumiApiException("Bangumi 请求超时，请稍后重试。", innerException: ex);
            }

            if (response.IsSuccessStatusCode || allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
            {
                return response;
            }

            if ((int)response.StatusCode >= 500 && attempt == 0)
            {
                response.Dispose();
                continue;
            }

            var error = CreateResponseException(response);
            response.Dispose();
            throw error;
        }

        throw new BangumiApiException("Bangumi 请求失败，请稍后重试。");
    }

    private static BangumiApiException CreateResponseException(HttpResponseMessage response)
    {
        var statusCode = response.StatusCode;
        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            var delay = response.Headers.RetryAfter?.Delta;
            if (delay is null && response.Headers.RetryAfter?.Date is DateTimeOffset retryAt)
            {
                delay = retryAt - DateTimeOffset.UtcNow;
            }

            var seconds = Math.Max(1, (int)Math.Ceiling(delay?.TotalSeconds ?? 60));
            return new BangumiApiException($"Bangumi 请求过于频繁，请在 {seconds} 秒后重试。", statusCode);
        }

        return statusCode switch
        {
            HttpStatusCode.Unauthorized => new BangumiApiException("Bangumi Access Token 无效或已撤销，请重新连接账号。", statusCode),
            HttpStatusCode.Forbidden => new BangumiApiException("Bangumi 拒绝了当前账号请求，请重新连接并检查权限。", statusCode),
            HttpStatusCode.NotFound => new BangumiApiException("Bangumi 条目不存在或已被移除。", statusCode),
            _ when (int)statusCode >= 500 => new BangumiApiException("Bangumi 服务暂时不可用，请稍后重试。", statusCode),
            _ => new BangumiApiException($"Bangumi 请求失败，服务器返回 {(int)statusCode}。", statusCode)
        };
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        clone.Version = request.Version;
        clone.VersionPolicy = request.VersionPolicy;
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            clone.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(cancellationToken));
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    private static async Task<JsonDocument> ReadDocumentAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static BangumiCollectionType ParseCollectionType(JsonElement root)
    {
        if (!root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.Number)
        {
            return BangumiCollectionType.None;
        }

        var value = type.GetInt32();
        return Enum.IsDefined(typeof(BangumiCollectionType), value)
            ? (BangumiCollectionType)value
            : BangumiCollectionType.None;
    }

    private static DateTime? ParseUtcDate(string value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed.UtcDateTime : null;
    }
}
