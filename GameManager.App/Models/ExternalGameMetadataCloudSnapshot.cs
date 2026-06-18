namespace GameManager.App.Models;

public sealed record ExternalGameMetadataCloudSnapshot
{
    public int SchemaVersion { get; init; } = 1;

    public string GameId { get; init; } = string.Empty;

    public string Provider { get; init; } = "bangumi";

    public string SubjectId { get; init; } = string.Empty;

    public bool IsLinked { get; init; } = true;

    public bool IsPartial { get; init; }

    public string OriginalName { get; init; } = string.Empty;

    public string LocalizedName { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string ReleaseDate { get; init; } = string.Empty;

    public string Developer { get; init; } = string.Empty;

    public string Publisher { get; init; } = string.Empty;

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string ImageUrl { get; init; } = string.Empty;

    public string SubjectUrl { get; init; } = string.Empty;

    public DateTime SourceUpdatedAtUtc { get; init; }

    public DateTime SnapshotUpdatedAtUtc { get; init; }

    public static ExternalGameMetadataCloudSnapshot FromMetadata(
        string gameId,
        ExternalGameMetadata metadata,
        DateTime snapshotUpdatedAtUtc)
    {
        return new ExternalGameMetadataCloudSnapshot
        {
            GameId = gameId,
            Provider = metadata.Provider,
            SubjectId = metadata.SubjectId,
            IsLinked = metadata.IsLinked,
            IsPartial = metadata.IsPartial,
            OriginalName = metadata.OriginalName,
            LocalizedName = metadata.LocalizedName,
            Summary = metadata.Summary,
            ReleaseDate = metadata.ReleaseDate,
            Developer = metadata.Developer,
            Publisher = metadata.Publisher,
            Tags = metadata.Tags.ToArray(),
            ImageUrl = metadata.ImageUrl,
            SubjectUrl = metadata.SubjectUrl,
            SourceUpdatedAtUtc = metadata.SourceUpdatedAtUtc.ToUniversalTime(),
            SnapshotUpdatedAtUtc = snapshotUpdatedAtUtc.ToUniversalTime()
        };
    }

    public ExternalGameMetadata ToMetadata()
    {
        return new ExternalGameMetadata
        {
            Provider = Provider,
            SubjectId = SubjectId,
            IsLinked = IsLinked,
            IsPartial = IsPartial,
            OriginalName = OriginalName,
            LocalizedName = LocalizedName,
            Summary = Summary,
            ReleaseDate = ReleaseDate,
            Developer = Developer,
            Publisher = Publisher,
            Tags = Tags.ToArray(),
            ImageUrl = ImageUrl,
            SubjectUrl = SubjectUrl,
            SourceUpdatedAtUtc = SourceUpdatedAtUtc.ToUniversalTime()
        };
    }
}
