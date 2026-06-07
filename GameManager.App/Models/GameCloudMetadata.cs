using System.IO;

namespace GameManager.App.Models;

public sealed class GameCloudMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CoverFileName { get; set; }
    public long TotalPlaySeconds { get; set; }
    public DateTime? LastLaunchTime { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public IReadOnlyList<string> PlaySessionIds { get; set; } = [];

    public static GameCloudMetadata FromGame(Game game)
    {
        return new GameCloudMetadata
        {
            Id = game.Id,
            Name = game.Name,
            CoverFileName = string.IsNullOrWhiteSpace(game.CoverImagePath)
                ? null
                : Path.GetFileName(game.CoverImagePath),
            TotalPlaySeconds = (long)game.TotalPlayTime.TotalSeconds,
            LastLaunchTime = game.LastLaunchTime,
            UpdatedAtUtc = game.UpdatedAtUtc
        };
    }
}
