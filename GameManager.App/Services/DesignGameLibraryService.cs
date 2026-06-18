using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class DesignGameLibraryService : IGameLibraryService
{
    private readonly InMemoryGameLibraryService inner = new();

    public IReadOnlyList<Game> GetGames()
    {
        return inner.GetGames();
    }

    public Game AddGame(AddGameRequest request)
    {
        return inner.AddGame(request);
    }

    public bool DeleteGame(string id)
    {
        return inner.DeleteGame(id);
    }

    public Game UpdateGame(UpdateGameRequest request)
    {
        return inner.UpdateGame(request);
    }

    public bool PinGameToTop(string id)
    {
        return inner.PinGameToTop(id);
    }

    public Game RecordLaunchResult(string id, LaunchResult result)
    {
        return inner.RecordLaunchResult(id, result);
    }

    public IReadOnlyList<PlaySession> GetPlaySessions(string gameId)
    {
        return inner.GetPlaySessions(gameId);
    }

    public ExternalGameMetadata? GetExternalMetadata(string gameId)
    {
        return inner.GetExternalMetadata(gameId);
    }

    public ExternalGameMetadataCloudSnapshot? GetExternalMetadataSnapshot(string gameId)
    {
        return inner.GetExternalMetadataSnapshot(gameId);
    }

    public IReadOnlyList<ExternalGameMetadataCloudSnapshot> GetExternalMetadataSnapshots()
    {
        return inner.GetExternalMetadataSnapshots();
    }

    public ExternalMetadataMergeResult ApplyCloudExternalMetadata(ExternalGameMetadataCloudSnapshot snapshot)
    {
        return inner.ApplyCloudExternalMetadata(snapshot);
    }

    public ExternalMetadataConflict? GetExternalMetadataConflict(string gameId)
    {
        return inner.GetExternalMetadataConflict(gameId);
    }

    public bool ClearExternalMetadataConflict(string gameId)
    {
        return inner.ClearExternalMetadataConflict(gameId);
    }

    public Game ResolveExternalMetadataConflict(string gameId, ExternalMetadataConflictResolution resolution)
    {
        return inner.ResolveExternalMetadataConflict(gameId, resolution);
    }

    public Game UpdateExternalMetadata(string gameId, ExternalGameMetadata? metadata)
    {
        return inner.UpdateExternalMetadata(gameId, metadata);
    }

    public BangumiCollectionState? GetBangumiCollectionState(string gameId)
    {
        return inner.GetBangumiCollectionState(gameId);
    }

    public void SaveBangumiCollectionState(BangumiCollectionState state)
    {
        inner.SaveBangumiCollectionState(state);
    }
}
