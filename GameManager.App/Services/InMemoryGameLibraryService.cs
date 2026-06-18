using GameManager.App.Models;

namespace GameManager.App.Services;

public sealed class InMemoryGameLibraryService : IGameLibraryService
{
    private readonly List<Game> games;
    private readonly List<PlaySession> playSessions = [];
    private readonly Dictionary<string, BangumiCollectionState> bangumiCollectionStates = [];
    private readonly Dictionary<string, ExternalMetadataConflict> externalMetadataConflicts = [];
    private readonly string machineId;

    public InMemoryGameLibraryService(string machineId = "in-memory")
    {
        this.machineId = machineId;
        games = CreateSampleGames();
    }

    public IReadOnlyList<Game> GetGames()
    {
        return games.ToList();
    }

    public Game AddGame(AddGameRequest request)
    {
        var game = new Game(
            Guid.NewGuid().ToString("N"),
            request.Name.Trim(),
            request.ExecutablePath.Trim(),
            request.GameRootPath.Trim(),
            request.SavePath.Trim(),
            string.IsNullOrWhiteSpace(request.CoverImagePath) ? null : request.CoverImagePath.Trim(),
            TimeSpan.Zero,
            null,
            request.LaunchArguments.Trim(),
            request.RunAsAdministrator,
            request.WorkingDirectory.Trim(),
            request.MonitorProcessName.Trim(),
            request.SyncEnabled,
            externalMetadata: request.ExternalMetadata);

        games.Add(game);
        return game;
    }

    public bool DeleteGame(string id)
    {
        var index = games.FindIndex(game => game.Id == id);
        if (index < 0)
        {
            return false;
        }

        games.RemoveAt(index);
        bangumiCollectionStates.Remove(id);
        externalMetadataConflicts.Remove(id);
        return true;
    }

    public Game UpdateGame(UpdateGameRequest request)
    {
        var index = games.FindIndex(game => game.Id == request.Id);
        if (index < 0)
        {
            throw new InvalidOperationException($"Game '{request.Id}' was not found.");
        }

        var existing = games[index];
        var normalizedCoverPath = string.IsNullOrWhiteSpace(request.CoverImagePath) ? null : request.CoverImagePath.Trim();
        var globalMetadataChanged =
            !string.Equals(existing.Name, request.Name.Trim(), StringComparison.Ordinal) ||
            !string.Equals(existing.CoverImagePath ?? string.Empty, normalizedCoverPath ?? string.Empty, StringComparison.Ordinal) ||
            request.ExternalMetadata is not null && !ExternalMetadataEquals(request.ExternalMetadata, existing.ExternalMetadata);
        var updated = new Game(
            existing.Id,
            request.Name.Trim(),
            request.ExecutablePath.Trim(),
            request.GameRootPath.Trim(),
            request.SavePath.Trim(),
            normalizedCoverPath,
            existing.TotalPlayTime,
            existing.LastLaunchTime,
            request.LaunchArguments.Trim(),
            request.RunAsAdministrator,
            request.WorkingDirectory.Trim(),
            request.MonitorProcessName.Trim(),
            request.SyncEnabled,
            globalMetadataChanged ? NextUpdateAt(existing.UpdatedAtUtc) : existing.UpdatedAtUtc,
            request.ExternalMetadata ?? existing.ExternalMetadata);

        games[index] = updated;
        return updated;
    }

    public IReadOnlyList<PlaySession> GetPlaySessions(string gameId)
    {
        return playSessions
            .Where(session => session.GameId == gameId)
            .OrderByDescending(session => session.StartedAt)
            .ToList();
    }

    public bool PinGameToTop(string id)
    {
        var index = games.FindIndex(game => game.Id == id);
        if (index <= 0)
        {
            return index == 0;
        }

        var game = games[index];
        games.RemoveAt(index);
        games.Insert(0, game);
        return true;
    }

    public Game RecordLaunchResult(string id, LaunchResult result)
    {
        var index = games.FindIndex(game => game.Id == id);
        if (index < 0)
        {
            throw new InvalidOperationException($"Game '{id}' was not found.");
        }

        var existing = games[index];
        var updated = new Game(
            existing.Id,
            existing.Name,
            existing.ExecutablePath,
            existing.GameRootPath,
            existing.SavePath,
            existing.CoverImagePath,
            existing.TotalPlayTime + result.Duration,
            result.LaunchedAt,
            existing.LaunchArguments,
            existing.RunAsAdministrator,
            existing.WorkingDirectory,
            existing.MonitorProcessName,
            existing.SyncEnabled,
            existing.UpdatedAtUtc,
            existing.ExternalMetadata);

        games[index] = updated;
        playSessions.Add(new PlaySession(
            Guid.NewGuid().ToString("N"),
            id,
            machineId,
            result.LaunchedAt,
            result.LaunchedAt + result.Duration,
            result.Duration,
            result.ExitCode));
        return updated;
    }

    public ExternalGameMetadata? GetExternalMetadata(string gameId)
    {
        return games.SingleOrDefault(game => game.Id == gameId)?.ExternalMetadata;
    }

    public ExternalGameMetadataCloudSnapshot? GetExternalMetadataSnapshot(string gameId)
    {
        var game = games.SingleOrDefault(item => item.Id == gameId);
        return game?.ExternalMetadata is null
            ? null
            : ExternalGameMetadataCloudSnapshot.FromMetadata(game.Id, game.ExternalMetadata, game.UpdatedAtUtc);
    }

    public IReadOnlyList<ExternalGameMetadataCloudSnapshot> GetExternalMetadataSnapshots()
    {
        return games
            .Where(game => game.ExternalMetadata is not null)
            .Select(game => ExternalGameMetadataCloudSnapshot.FromMetadata(game.Id, game.ExternalMetadata!, game.UpdatedAtUtc))
            .ToList();
    }

    public ExternalMetadataMergeResult ApplyCloudExternalMetadata(ExternalGameMetadataCloudSnapshot snapshot)
    {
        var index = games.FindIndex(game => game.Id == snapshot.GameId);
        if (index < 0)
        {
            return new ExternalMetadataMergeResult(
                ExternalMetadataMergeStatus.MissingGame,
                $"Game '{snapshot.GameId}' was not found.");
        }

        var existing = GetExternalMetadataSnapshot(snapshot.GameId);
        if (existing is not null &&
            (!string.Equals(existing.Provider, snapshot.Provider, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(existing.SubjectId, snapshot.SubjectId, StringComparison.OrdinalIgnoreCase)))
        {
            var reason = CreateConflictReason(snapshot.GameId, existing, snapshot);
            externalMetadataConflicts[snapshot.GameId] = new ExternalMetadataConflict(
                snapshot.GameId,
                existing,
                snapshot,
                DateTime.UtcNow,
                reason);
            return new ExternalMetadataMergeResult(
                ExternalMetadataMergeStatus.Conflict,
                reason);
        }

        if (existing is not null &&
            existing.SnapshotUpdatedAtUtc.ToUniversalTime() >= snapshot.SnapshotUpdatedAtUtc.ToUniversalTime())
        {
            return new ExternalMetadataMergeResult(
                ExternalMetadataMergeStatus.KeptLocal,
                $"Local external metadata for '{snapshot.GameId}' is newer.");
        }

        var game = games[index];
        games[index] = WithExternalMetadata(game, snapshot.ToMetadata(), snapshot.SnapshotUpdatedAtUtc);
        externalMetadataConflicts.Remove(snapshot.GameId);
        return new ExternalMetadataMergeResult(
            ExternalMetadataMergeStatus.Applied,
            $"Cloud external metadata for '{snapshot.GameId}' was applied.");
    }

    public ExternalMetadataConflict? GetExternalMetadataConflict(string gameId)
    {
        return externalMetadataConflicts.GetValueOrDefault(gameId);
    }

    public bool ClearExternalMetadataConflict(string gameId)
    {
        return externalMetadataConflicts.Remove(gameId);
    }

    public Game ResolveExternalMetadataConflict(string gameId, ExternalMetadataConflictResolution resolution)
    {
        var conflict = GetExternalMetadataConflict(gameId)
            ?? throw new InvalidOperationException($"External metadata conflict for '{gameId}' was not found.");
        var index = games.FindIndex(game => game.Id == gameId);
        if (index < 0)
        {
            throw new InvalidOperationException($"Game '{gameId}' was not found.");
        }

        var game = games[index];
        var resolved = resolution switch
        {
            ExternalMetadataConflictResolution.UseCloud => WithExternalMetadata(
                game,
                conflict.CloudSnapshot.ToMetadata(),
                conflict.CloudSnapshot.SnapshotUpdatedAtUtc),
            ExternalMetadataConflictResolution.UnlinkLocal when game.ExternalMetadata is not null => WithExternalMetadata(
                game,
                game.ExternalMetadata with { IsLinked = false },
                NextUpdateAt(game.UpdatedAtUtc)),
            _ => game
        };
        games[index] = resolved;
        externalMetadataConflicts.Remove(gameId);
        return resolved;
    }

    private static Game WithExternalMetadata(Game game, ExternalGameMetadata? metadata, DateTime updatedAtUtc)
    {
        return new Game(
            game.Id,
            game.Name,
            game.ExecutablePath,
            game.GameRootPath,
            game.SavePath,
            game.CoverImagePath,
            game.TotalPlayTime,
            game.LastLaunchTime,
            game.LaunchArguments,
            game.RunAsAdministrator,
            game.WorkingDirectory,
            game.MonitorProcessName,
            game.SyncEnabled,
            updatedAtUtc,
            metadata);
    }

    public Game UpdateExternalMetadata(string gameId, ExternalGameMetadata? metadata)
    {
        var index = games.FindIndex(game => game.Id == gameId);
        if (index < 0)
        {
            throw new InvalidOperationException($"Game '{gameId}' was not found.");
        }

        var existing = games[index];
        var updated = new Game(
            existing.Id,
            existing.Name,
            existing.ExecutablePath,
            existing.GameRootPath,
            existing.SavePath,
            existing.CoverImagePath,
            existing.TotalPlayTime,
            existing.LastLaunchTime,
            existing.LaunchArguments,
            existing.RunAsAdministrator,
            existing.WorkingDirectory,
            existing.MonitorProcessName,
            existing.SyncEnabled,
            NextUpdateAt(existing.UpdatedAtUtc),
            metadata);
        games[index] = updated;
        externalMetadataConflicts.Remove(gameId);
        return updated;
    }

    public BangumiCollectionState? GetBangumiCollectionState(string gameId)
    {
        return bangumiCollectionStates.GetValueOrDefault(gameId);
    }

    public void SaveBangumiCollectionState(BangumiCollectionState state)
    {
        if (!games.Any(game => game.Id == state.GameId))
        {
            throw new InvalidOperationException($"Game '{state.GameId}' was not found.");
        }

        bangumiCollectionStates[state.GameId] = state;
    }

    private static DateTime NextUpdateAt(DateTime existing)
    {
        var now = DateTime.UtcNow;
        return now > existing ? now : existing.AddTicks(1);
    }

    private static bool ExternalMetadataEquals(ExternalGameMetadata left, ExternalGameMetadata? right)
    {
        return right is not null &&
            left.Provider == right.Provider &&
            left.SubjectId == right.SubjectId &&
            left.IsLinked == right.IsLinked &&
            left.OriginalName == right.OriginalName &&
            left.LocalizedName == right.LocalizedName &&
            left.Summary == right.Summary &&
            left.ReleaseDate == right.ReleaseDate &&
            left.Developer == right.Developer &&
            left.Publisher == right.Publisher &&
            left.Tags.SequenceEqual(right.Tags, StringComparer.Ordinal) &&
            left.ImageUrl == right.ImageUrl &&
            left.SubjectUrl == right.SubjectUrl &&
            left.SourceUpdatedAtUtc.ToUniversalTime() == right.SourceUpdatedAtUtc.ToUniversalTime();
    }

    private static string CreateConflictReason(
        string gameId,
        ExternalGameMetadataCloudSnapshot local,
        ExternalGameMetadataCloudSnapshot cloud)
    {
        return $"External metadata conflict for '{gameId}': local {local.Provider}/{local.SubjectId}, cloud {cloud.Provider}/{cloud.SubjectId}.";
    }

    private static List<Game> CreateSampleGames()
    {
        return
        [
            new Game(
                "sample-celeste",
                "Celeste",
                @"D:\Games\Celeste\Celeste.exe",
                @"D:\Games\Celeste",
                @"C:\Users\Public\Saved Games\Celeste",
                null,
                TimeSpan.FromHours(18.5),
                DateTime.Today.AddDays(-2).AddHours(21)),
            new Game(
                "sample-hades",
                "Hades",
                @"D:\Games\Hades\Hades.exe",
                @"D:\Games\Hades",
                @"C:\Users\Public\Saved Games\Hades",
                null,
                TimeSpan.FromHours(42).Add(TimeSpan.FromMinutes(15)),
                DateTime.Today.AddDays(-8).AddHours(20)),
            new Game(
                "sample-gris",
                "Gris",
                @"D:\Games\Gris\Gris.exe",
                @"D:\Games\Gris",
                @"C:\Users\Public\Saved Games\Gris",
                null,
                TimeSpan.FromHours(5).Add(TimeSpan.FromMinutes(40)),
                DateTime.Today.AddMonths(-1).AddHours(19))
        ];
    }
}
