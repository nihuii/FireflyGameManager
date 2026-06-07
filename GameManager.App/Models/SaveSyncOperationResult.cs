namespace GameManager.App.Models;

public sealed record SaveSyncOperationResult(
    bool Success,
    bool HasConflict,
    string Message,
    bool RequiresCloudDownloadConfirmation = false)
{
    public bool RequiresUserAction => HasConflict || RequiresCloudDownloadConfirmation;
}

public enum SaveConflictResolution
{
    UseLocal,
    UseCloud,
    KeepBoth,
    Cancel
}
