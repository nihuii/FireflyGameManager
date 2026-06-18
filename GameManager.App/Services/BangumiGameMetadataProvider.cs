using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class BangumiGameMetadataProvider : IGameMetadataProvider
{
    private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromMinutes(10);
    private readonly IBangumiApiClient client;
    private readonly IBangumiAccountStore? accountStore;
    private readonly Dictionary<string, (DateTime ExpiresAtUtc, IReadOnlyList<GameMetadataSearchResult> Results)> searchCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock cacheLock = new();

    public BangumiGameMetadataProvider(IBangumiApiClient client, IBangumiAccountStore? accountStore = null)
    {
        this.client = client;
        this.accountStore = accountStore;
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

    public async Task<ExternalGameMetadata?> GetDetailsAsync(
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        return (await LookupDetailsAsync(subjectId, string.Empty, cancellationToken)).Metadata;
    }

    public async Task<GameMetadataLookupResult> LookupDetailsAsync(
        string subjectId,
        string fallbackQuery,
        CancellationToken cancellationToken = default)
    {
        var account = accountStore?.Load();
        var reconnectRequired = accountStore is not null && (account is null || account.RequiresReconnect);
        var authenticatedRequestRejected = false;
        ExternalGameMetadata? metadata = null;

        if (account is { RequiresReconnect: false } && !string.IsNullOrWhiteSpace(account.AccessToken))
        {
            try
            {
                metadata = await client.GetGameDetailsAsync(subjectId, account.AccessToken, cancellationToken);
            }
            catch (BangumiApiException ex) when (ex.IsAuthenticationFailure)
            {
                reconnectRequired = true;
                authenticatedRequestRejected = true;
                accountStore?.Save(account with { RequiresReconnect = true });
            }
        }
        else
        {
            metadata = await client.GetGameDetailsAsync(subjectId, cancellationToken);
        }

        if (metadata is null && authenticatedRequestRejected)
        {
            metadata = await client.GetGameDetailsAsync(subjectId, cancellationToken);
        }

        if (metadata is not null)
        {
            return new GameMetadataLookupResult(
                metadata with { IsPartial = false },
                reconnectRequired
                    ? GameMetadataLookupStatus.AccountReconnectRequired
                    : GameMetadataLookupStatus.Complete);
        }

        if (!string.IsNullOrWhiteSpace(fallbackQuery))
        {
            var result = (await client.SearchLegacyGamesAsync(fallbackQuery.Trim(), cancellationToken))
                .FirstOrDefault(item => string.Equals(item.SubjectId, subjectId, StringComparison.OrdinalIgnoreCase));
            if (result is not null)
            {
                var partial = new ExternalGameMetadata
                {
                    Provider = result.Provider,
                    SubjectId = result.SubjectId,
                    IsLinked = true,
                    IsPartial = true,
                    OriginalName = result.Name,
                    LocalizedName = result.LocalizedName,
                    Summary = result.SummaryPreview,
                    ReleaseDate = result.ReleaseDate,
                    ImageUrl = result.ImageUrl,
                    SubjectUrl = $"https://bgm.tv/subject/{Uri.EscapeDataString(result.SubjectId)}",
                    SourceUpdatedAtUtc = DateTime.UtcNow
                };
                return new GameMetadataLookupResult(
                    partial,
                    reconnectRequired
                        ? GameMetadataLookupStatus.AccountReconnectRequired
                        : GameMetadataLookupStatus.Partial);
            }
        }

        return new GameMetadataLookupResult(
            null,
            reconnectRequired
                ? GameMetadataLookupStatus.AccountReconnectRequired
                : GameMetadataLookupStatus.NotFound);
    }
}
