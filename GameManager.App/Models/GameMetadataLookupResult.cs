namespace GameManager.App.Models;

public enum GameMetadataLookupStatus
{
    Complete,
    Partial,
    AccountReconnectRequired,
    NotFound
}

public sealed record GameMetadataLookupResult(
    ExternalGameMetadata? Metadata,
    GameMetadataLookupStatus Status);
