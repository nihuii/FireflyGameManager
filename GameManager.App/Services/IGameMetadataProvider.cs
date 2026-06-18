using GameManager.App.Models;

namespace GameManager.App.Services;

public interface IGameMetadataProvider
{
    string ProviderId { get; }

    Task<IReadOnlyList<GameMetadataSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<ExternalGameMetadata?> GetDetailsAsync(
        string subjectId,
        CancellationToken cancellationToken = default);

    async Task<GameMetadataLookupResult> LookupDetailsAsync(
        string subjectId,
        string fallbackQuery,
        CancellationToken cancellationToken = default)
    {
        var metadata = await GetDetailsAsync(subjectId, cancellationToken);
        return new GameMetadataLookupResult(
            metadata,
            metadata is null
                ? GameMetadataLookupStatus.NotFound
                : metadata.IsPartial
                    ? GameMetadataLookupStatus.Partial
                    : GameMetadataLookupStatus.Complete);
    }
}
