using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class BangumiGameMetadataProvider : IGameMetadataProvider
{
    private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromMinutes(10);
    private readonly IBangumiApiClient client;
    private readonly Dictionary<string, (DateTime ExpiresAtUtc, IReadOnlyList<GameMetadataSearchResult> Results)> searchCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock cacheLock = new();

    public BangumiGameMetadataProvider(IBangumiApiClient client)
    {
        this.client = client;
    }

    public string ProviderId => "bangumi";

    public async Task<IReadOnlyList<GameMetadataSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var normalized = query.Trim();
        lock (cacheLock)
        {
            if (searchCache.TryGetValue(normalized, out var cached) && cached.ExpiresAtUtc > DateTime.UtcNow)
            {
                return cached.Results;
            }
        }

        var results = await client.SearchGamesAsync(normalized, cancellationToken);
        lock (cacheLock)
        {
            searchCache[normalized] = (DateTime.UtcNow.Add(SearchCacheDuration), results);
        }

        return results;
    }

    public Task<ExternalGameMetadata?> GetDetailsAsync(
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        return client.GetGameDetailsAsync(subjectId, cancellationToken);
    }
}
