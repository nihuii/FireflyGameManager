using GameManager.App.Models;

namespace GameManager.App.Services;

public interface IBangumiApiClient
{
    Task<BangumiAccount> GetCurrentUserAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameMetadataSearchResult>> SearchGamesAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<ExternalGameMetadata?> GetGameDetailsAsync(
        string subjectId,
        CancellationToken cancellationToken = default);

    Task<BangumiCollectionState?> GetCollectionAsync(
        BangumiAccount account,
        string gameId,
        string subjectId,
        CancellationToken cancellationToken = default);

    Task<BangumiCollectionState> SaveCollectionAsync(
        BangumiAccount account,
        BangumiCollectionState state,
        bool isExistingCollection = false,
        CancellationToken cancellationToken = default);
}
