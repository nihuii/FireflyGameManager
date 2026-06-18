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
}
