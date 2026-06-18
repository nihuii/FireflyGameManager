using GameManager.App.Models;

namespace GameManager.App.Services;

public interface IGameLibraryService
{
    IReadOnlyList<Game> GetGames();

    Game AddGame(AddGameRequest request);

    bool DeleteGame(string id);

    Game UpdateGame(UpdateGameRequest request);

    bool PinGameToTop(string id);

    Game RecordLaunchResult(string id, LaunchResult result);

    IReadOnlyList<PlaySession> GetPlaySessions(string gameId);

    ExternalGameMetadata? GetExternalMetadata(string gameId);

    ExternalGameMetadataCloudSnapshot? GetExternalMetadataSnapshot(string gameId);

    IReadOnlyList<ExternalGameMetadataCloudSnapshot> GetExternalMetadataSnapshots();

    ExternalMetadataMergeResult ApplyCloudExternalMetadata(ExternalGameMetadataCloudSnapshot snapshot);

    ExternalMetadataConflict? GetExternalMetadataConflict(string gameId);

    bool ClearExternalMetadataConflict(string gameId);

    Game ResolveExternalMetadataConflict(string gameId, ExternalMetadataConflictResolution resolution);

    Game UpdateExternalMetadata(string gameId, ExternalGameMetadata? metadata);

    BangumiCollectionState? GetBangumiCollectionState(string gameId);

    void SaveBangumiCollectionState(BangumiCollectionState state);
}
