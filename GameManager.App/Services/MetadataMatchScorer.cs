using System.Globalization;
using System.Text;
using GameManager.App.Models;

namespace GameManager.App.Services;

public static class MetadataMatchScorer
{
    public const int NoMatchScore = 100;

    public static bool IsExactMatch(string query, GameMetadataSearchResult result)
    {
        var normalizedQuery = Normalize(query);
        return normalizedQuery.Length > 0 && GetTitles(result)
            .Select(Normalize)
            .Any(title => string.Equals(title, normalizedQuery, StringComparison.OrdinalIgnoreCase));
    }

    public static int Score(string query, GameMetadataSearchResult result)
    {
        var normalizedQuery = Normalize(query);
        if (normalizedQuery.Length == 0)
        {
            return NoMatchScore;
        }

        var titles = GetTitles(result).ToList();
        var normalizedTitles = titles.Select(Normalize).Where(value => value.Length > 0).ToList();
        if (normalizedTitles.Any(title => string.Equals(title, normalizedQuery, StringComparison.OrdinalIgnoreCase)))
        {
            return 0;
        }

        if (normalizedTitles.Any(title => title.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
        {
            return 1;
        }

        var queryTokens = Tokenize(query);
        if (queryTokens.Count > 1 && normalizedTitles.Any(title => queryTokens.All(token =>
                title.Contains(token, StringComparison.OrdinalIgnoreCase))))
        {
            return 2;
        }

        if (normalizedTitles.Any(title => title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
        {
            return 3;
        }

        return NoMatchScore;
    }

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (char.IsLetterOrDigit(character) ||
                category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> Tokenize(string value)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        foreach (var character in value.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Append(character);
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens
            .Select(Normalize)
            .Where(token => token.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> GetTitles(GameMetadataSearchResult result)
    {
        return new[] { result.DisplayName, result.LocalizedName, result.Name }
            .Concat(result.Aliases)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
