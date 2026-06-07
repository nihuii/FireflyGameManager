namespace GameManager.App.Models;

public sealed class GameLibraryMergeResult
{
    public GameLibraryMergeResult(int addedCount, int updatedCount)
    {
        AddedCount = addedCount;
        UpdatedCount = updatedCount;
    }

    public int AddedCount { get; }

    public int UpdatedCount { get; }
}
