namespace GameManager.App.Models;

public sealed record ExternalGameMetadata
{
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
}
