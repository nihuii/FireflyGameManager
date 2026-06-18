namespace GameManager.App.Models;

public sealed record MetadataImportOptions(
    bool ImportName,
    bool ImportCover,
    bool ImportSummary,
    bool ImportReleaseDate,
    bool ImportDeveloper,
    bool ImportPublisher,
    bool ImportTags)
{
    public static MetadataImportOptions ForNewGame { get; } = new(true, true, true, true, true, true, true);

    public static MetadataImportOptions ForExistingGame { get; } = new(false, false, true, true, true, true, true);

    public ExternalGameMetadata Merge(ExternalGameMetadata? existing, ExternalGameMetadata incoming)
    {
        return incoming with
        {
            IsLinked = incoming.IsLinked,
            OriginalName = MergeIdentityName(ImportName, incoming.OriginalName, existing?.OriginalName),
            LocalizedName = MergeIdentityName(ImportName, incoming.LocalizedName, existing?.LocalizedName),
            Summary = ImportSummary ? incoming.Summary : existing?.Summary ?? string.Empty,
            ReleaseDate = ImportReleaseDate ? incoming.ReleaseDate : existing?.ReleaseDate ?? string.Empty,
            Developer = ImportDeveloper ? incoming.Developer : existing?.Developer ?? string.Empty,
            Publisher = ImportPublisher ? incoming.Publisher : existing?.Publisher ?? string.Empty,
            Tags = ImportTags ? incoming.Tags : existing?.Tags ?? [],
            ImageUrl = ImportCover ? incoming.ImageUrl : existing?.ImageUrl ?? string.Empty
        };
    }

    private static string MergeIdentityName(bool importName, string incoming, string? existing)
    {
        if (importName || string.IsNullOrWhiteSpace(existing))
        {
            return incoming;
        }

        return existing;
    }
}
