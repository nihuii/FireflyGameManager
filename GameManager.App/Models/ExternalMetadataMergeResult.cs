namespace GameManager.App.Models;

public enum ExternalMetadataMergeStatus
{
    Applied,
    KeptLocal,
    Conflict,
    MissingGame,
    Invalid
}

public sealed record ExternalMetadataMergeResult(ExternalMetadataMergeStatus Status, string Message)
{
    public bool Applied => Status == ExternalMetadataMergeStatus.Applied;

    public bool IsConflict => Status == ExternalMetadataMergeStatus.Conflict;
}
