namespace GameManager.App.Models;

public enum ExternalMetadataConflictResolution
{
    UseLocal,
    UseCloud,
    UnlinkLocal
}

public sealed record ExternalMetadataConflict(
    string GameId,
    ExternalGameMetadataCloudSnapshot LocalSnapshot,
    ExternalGameMetadataCloudSnapshot CloudSnapshot,
    DateTime DetectedAtUtc,
    string Reason);
