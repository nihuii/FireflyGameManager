using System.IO;
using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class LocalGameDiscoveryService : IGameDiscoveryService
{
    private static readonly string[] ExcludedNameParts =
    [
        "unins",
        "uninstall",
        "setup",
        "installer",
        "crashreport",
        "crashhandler",
        "vc_redist",
        "dxsetup"
    ];

    public IReadOnlyList<AddGameRequest> Discover(string rootDirectory, IEnumerable<string> existingExecutablePaths)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            return [];
        }

        var existing = new HashSet<string>(
            existingExecutablePaths.Select(NormalizePath),
            StringComparer.OrdinalIgnoreCase);

        return Directory
            .EnumerateFiles(rootDirectory, "*.exe", SearchOption.AllDirectories)
            .Where(path => !existing.Contains(NormalizePath(path)))
            .Where(IsGameCandidate)
            .Select(path =>
            {
                var directory = Path.GetDirectoryName(path) ?? rootDirectory;
                return new AddGameRequest(
                    Path.GetFileNameWithoutExtension(path),
                    path,
                    directory,
                    string.Empty,
                    null);
            })
            .ToList();
    }

    private static bool IsGameCandidate(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return !ExcludedNameParts.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
