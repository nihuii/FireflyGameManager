namespace GameManager.App.Models;

public sealed record GameMetadataSearchResult(
    string Provider,
    string SubjectId,
    string Name,
    string LocalizedName,
    string ReleaseDate,
    string ImageUrl,
    string SummaryPreview)
{
    public IReadOnlyList<string> Aliases { get; init; } = [];

    public string Developer { get; init; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(LocalizedName) ? Name : LocalizedName;

    public string AuxiliaryInfo => string.Join(" · ", new[]
    {
        Provider,
        string.Equals(DisplayName, Name, StringComparison.OrdinalIgnoreCase) ? string.Empty : Name,
        ReleaseDate,
        Developer
    }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public string CompactSummaryPreview => string.IsNullOrWhiteSpace(SummaryPreview)
        ? string.Empty
        : string.Join(' ', SummaryPreview.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
