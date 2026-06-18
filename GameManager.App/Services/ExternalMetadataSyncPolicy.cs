using GameManager.App.Models;

namespace GameManager.App.Services;

public static class ExternalMetadataSyncPolicy
{
    public static bool CanUpload(IGameLibraryService library, string gameId)
    {
        return library.GetExternalMetadataConflict(gameId) is null;
    }

    public static string Describe(ExternalGameMetadataCloudSnapshot snapshot, string outcome)
    {
        return $"{outcome}：provider={snapshot.Provider}，subject={snapshot.SubjectId}，game={snapshot.GameId}";
    }
}
