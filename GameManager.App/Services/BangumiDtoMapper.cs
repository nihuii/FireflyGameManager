using System.Text.Json;
using GameManager.App.Models;

namespace GameManager.App.Services;

internal static class BangumiDtoMapper
{
    public static GameMetadataSearchResult ToSearchResult(JsonElement subject)
    {
        var originalName = GetString(subject, "name");
        var localizedName = GetString(subject, "name_cn");
        return new GameMetadataSearchResult(
            "bangumi",
            GetSubjectId(subject),
            originalName,
            localizedName,
            GetDate(subject),
            GetImageUrl(subject),
            GetString(subject, "summary"))
        {
            Aliases = GetAliases(subject),
            Developer = GetInfoboxValue(subject, "开发", "开发商", "游戏开发", "Developer")
        };
    }

    public static ExternalGameMetadata ToMetadata(JsonElement subject)
    {
        return new ExternalGameMetadata
        {
            Provider = "bangumi",
            SubjectId = GetSubjectId(subject),
            OriginalName = GetString(subject, "name"),
            LocalizedName = GetString(subject, "name_cn"),
            Summary = GetString(subject, "summary"),
            ReleaseDate = GetDate(subject),
            Developer = GetInfoboxValue(subject, "开发", "开发商", "游戏开发", "Developer"),
            Publisher = GetInfoboxValue(subject, "发行", "发行商", "游戏发行", "Publisher"),
            Tags = GetTags(subject),
            ImageUrl = GetImageUrl(subject),
            SubjectUrl = $"https://bgm.tv/subject/{GetSubjectId(subject)}",
            SourceUpdatedAtUtc = DateTime.UtcNow
        };
    }

    public static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    public static string GetIntOrString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return string.Empty;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.GetInt64().ToString(),
            JsonValueKind.String => property.GetString() ?? string.Empty,
            _ => string.Empty
        };
    }

    private static string GetSubjectId(JsonElement subject)
    {
        foreach (var propertyName in new[] { "id", "subject_id", "subjectId" })
        {
            var value = GetIntOrString(subject, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string GetDate(JsonElement subject)
    {
        var date = GetString(subject, "date");
        return string.IsNullOrWhiteSpace(date) ? GetString(subject, "air_date") : date;
    }

    private static string GetImageUrl(JsonElement subject)
    {
        if (!subject.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        foreach (var key in new[] { "large", "common", "medium", "small", "grid" })
        {
            var value = GetString(images, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return NormalizeImageUrl(value);
            }
        }

        return string.Empty;
    }

    private static string NormalizeImageUrl(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return $"https:{trimmed}";
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(uri.Host, "bgm.tv", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.EndsWith(".bgm.tv", StringComparison.OrdinalIgnoreCase)))
        {
            return new UriBuilder(uri)
            {
                Scheme = Uri.UriSchemeHttps,
                Port = -1
            }.Uri.AbsoluteUri;
        }

        return trimmed;
    }

    private static IReadOnlyList<string> GetTags(JsonElement subject)
    {
        if (!subject.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return tags.EnumerateArray()
            .Select(tag => GetString(tag, "name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> GetAliases(JsonElement subject)
    {
        var aliases = new List<string>();
        if (subject.TryGetProperty("aliases", out var directAliases) && directAliases.ValueKind == JsonValueKind.Array)
        {
            aliases.AddRange(directAliases.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString() ?? string.Empty));
        }

        if (subject.TryGetProperty("infobox", out var infobox) && infobox.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in infobox.EnumerateArray())
            {
                var key = GetString(item, "key");
                if (key is not ("别名" or "Alias" or "Aliases") || !item.TryGetProperty("value", out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.String)
                {
                    aliases.Add(value.GetString() ?? string.Empty);
                }
                else if (value.ValueKind == JsonValueKind.Array)
                {
                    aliases.AddRange(value.EnumerateArray().Select(entry =>
                        entry.ValueKind == JsonValueKind.String ? entry.GetString() ?? string.Empty : GetString(entry, "v")));
                }
            }
        }

        return aliases
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetInfoboxValue(JsonElement subject, params string[] keys)
    {
        if (!subject.TryGetProperty("infobox", out var infobox) || infobox.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var item in infobox.EnumerateArray())
        {
            var key = GetString(item, "key");
            if (!keys.Contains(key, StringComparer.OrdinalIgnoreCase) ||
                !item.TryGetProperty("value", out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                return string.Join(" / ", value.EnumerateArray()
                    .Select(entry => entry.ValueKind == JsonValueKind.Object ? GetString(entry, "v") : string.Empty)
                    .Where(text => !string.IsNullOrWhiteSpace(text)));
            }
        }

        return string.Empty;
    }
}
