using System.IO;
using System.Text.Json;
using GameManager.App.Models;
using Microsoft.Data.Sqlite;

namespace GameManager.App.Services;

public sealed class SqliteGameLibraryService : IGameLibraryService
{
    private readonly string databasePath;
    private readonly string connectionString;
    private readonly string machineId;

    public SqliteGameLibraryService(string databasePath, string? machineId = null)
    {
        this.databasePath = databasePath;
        this.machineId = string.IsNullOrWhiteSpace(machineId) ? Environment.MachineName : machineId.Trim();
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();

        BackupLegacyDatabaseIfNeeded();
        InitializeDatabase();
    }

    public IReadOnlyList<Game> GetGames()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id,
                   name,
                   executable_path,
                   game_root_path,
                   save_path,
                   cover_image_path,
                   total_play_seconds,
                   last_launch_time,
                   launch_arguments,
                   run_as_administrator,
                   working_directory,
                   monitor_process_name,
                   sync_enabled,
                   updated_at
            FROM games
            ORDER BY sort_order ASC, created_at ASC;
            """;

        var games = new List<Game>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                games.Add(ReadGame(reader));
            }
        }

        return games
            .Select(game => WithExternalMetadata(game, ReadExternalMetadata(connection, game.Id)))
            .ToList();
    }

    public Game AddGame(AddGameRequest request)
    {
        var now = DateTime.UtcNow;
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
            now,
            request.ExternalMetadata);
        var nowText = now.ToString("O");

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO games (
                id,
                name,
                executable_path,
                game_root_path,
                save_path,
                cover_image_path,
                total_play_seconds,
                last_launch_time,
                launch_arguments,
                run_as_administrator,
                working_directory,
                monitor_process_name,
                sync_enabled,
                sort_order,
                created_at,
                updated_at
            )
            VALUES (
                $id,
                $name,
                $executablePath,
                $gameRootPath,
                $savePath,
                $coverImagePath,
                $totalPlaySeconds,
                $lastLaunchTime,
                $launchArguments,
                $runAsAdministrator,
                $workingDirectory,
                $monitorProcessName,
                $syncEnabled,
                $sortOrder,
                $createdAt,
                $updatedAt
            );
            """;
        AddGameParameters(command, game);
        command.Parameters.AddWithValue("$sortOrder", GetNextSortOrder(connection));
        command.Parameters.AddWithValue("$createdAt", nowText);
        command.Parameters.AddWithValue("$updatedAt", nowText);
        command.ExecuteNonQuery();
        if (request.ExternalMetadata is not null)
        {
            WriteExternalMetadata(connection, game.Id, request.ExternalMetadata);
        }

        return game;
    }

    public bool DeleteGame(string id)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        DeleteRelatedGameData(connection, transaction, id);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM games WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        var affectedRows = command.ExecuteNonQuery();
        if (affectedRows > 0)
        {
            using var tombstone = connection.CreateCommand();
            tombstone.Transaction = transaction;
            tombstone.CommandText =
                """
                INSERT INTO deleted_games (id, deleted_at)
                VALUES ($id, $deletedAt)
                ON CONFLICT(id) DO UPDATE SET deleted_at = excluded.deleted_at;
                """;
            tombstone.Parameters.AddWithValue("$id", id);
            tombstone.Parameters.AddWithValue("$deletedAt", DateTimeOffset.UtcNow.ToString("O"));
            tombstone.ExecuteNonQuery();
            ReindexSortOrder(connection, transaction);
        }

        transaction.Commit();
        return affectedRows > 0;
    }

    public Game UpdateGame(UpdateGameRequest request)
    {
        using var connection = OpenConnection();
        var existing = GetGameById(connection, request.Id)
            ?? throw new InvalidOperationException($"Game '{request.Id}' was not found.");
        var globalMetadataChanged =
            !string.Equals(existing.Name, request.Name.Trim(), StringComparison.Ordinal) ||
            !string.Equals(
                existing.CoverImagePath ?? string.Empty,
                string.IsNullOrWhiteSpace(request.CoverImagePath) ? string.Empty : request.CoverImagePath.Trim(),
                StringComparison.Ordinal) ||
            request.ExternalMetadata is not null && !ExternalMetadataEquals(request.ExternalMetadata, existing.ExternalMetadata);
        var updatedAt = globalMetadataChanged ? DateTime.UtcNow : existing.UpdatedAtUtc;
        var updated = new Game(
            existing.Id,
            request.Name.Trim(),
            request.ExecutablePath.Trim(),
            request.GameRootPath.Trim(),
            request.SavePath.Trim(),
            string.IsNullOrWhiteSpace(request.CoverImagePath) ? null : request.CoverImagePath.Trim(),
            existing.TotalPlayTime,
            existing.LastLaunchTime,
            request.LaunchArguments.Trim(),
            request.RunAsAdministrator,
            request.WorkingDirectory.Trim(),
            request.MonitorProcessName.Trim(),
            request.SyncEnabled,
            updatedAt,
            request.ExternalMetadata ?? existing.ExternalMetadata);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE games
            SET name = $name,
                executable_path = $executablePath,
                game_root_path = $gameRootPath,
                save_path = $savePath,
                cover_image_path = $coverImagePath,
                launch_arguments = $launchArguments,
                run_as_administrator = $runAsAdministrator,
                working_directory = $workingDirectory,
                monitor_process_name = $monitorProcessName,
                sync_enabled = $syncEnabled,
                updated_at = $updatedAt
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", updated.Id);
        command.Parameters.AddWithValue("$name", updated.Name);
        command.Parameters.AddWithValue("$executablePath", updated.ExecutablePath);
        command.Parameters.AddWithValue("$gameRootPath", updated.GameRootPath);
        command.Parameters.AddWithValue("$savePath", updated.SavePath);
        command.Parameters.AddWithValue("$coverImagePath", ToDbValue(updated.CoverImagePath));
        command.Parameters.AddWithValue("$launchArguments", updated.LaunchArguments);
        command.Parameters.AddWithValue("$runAsAdministrator", updated.RunAsAdministrator ? 1 : 0);
        command.Parameters.AddWithValue("$workingDirectory", updated.WorkingDirectory);
        command.Parameters.AddWithValue("$monitorProcessName", updated.MonitorProcessName);
        command.Parameters.AddWithValue("$syncEnabled", updated.SyncEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAt", updatedAt.ToString("O"));
        command.ExecuteNonQuery();
        if (request.ExternalMetadata is not null)
        {
            WriteExternalMetadata(connection, updated.Id, request.ExternalMetadata);
        }

        return updated;
    }

    public bool PinGameToTop(string id)
    {
        using var connection = OpenConnection();
        var orderedIds = GetOrderedIds(connection);
        var index = orderedIds.IndexOf(id);
        if (index < 0)
        {
            return false;
        }

        if (index == 0)
        {
            return true;
        }

        orderedIds.RemoveAt(index);
        orderedIds.Insert(0, id);

        using var transaction = connection.BeginTransaction();
        UpdateSortOrder(connection, transaction, orderedIds);
        transaction.Commit();
        return true;
    }

    public Game RecordLaunchResult(string id, LaunchResult result)
    {
        using var connection = OpenConnection();
        var existing = GetGameById(connection, id)
            ?? throw new InvalidOperationException($"Game '{id}' was not found.");
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

        using var transaction = connection.BeginTransaction();
        using var sessionCommand = connection.CreateCommand();
        sessionCommand.Transaction = transaction;
        sessionCommand.CommandText =
            """
            INSERT INTO play_sessions (
                id, game_id, machine_id, start_time, end_time, duration_seconds,
                exit_code, synced, created_at
            )
            VALUES (
                $id, $gameId, $machineId, $startTime, $endTime, $durationSeconds,
                $exitCode, 0, $createdAt
            );
            """;
        sessionCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        sessionCommand.Parameters.AddWithValue("$gameId", id);
        sessionCommand.Parameters.AddWithValue("$machineId", machineId);
        sessionCommand.Parameters.AddWithValue("$startTime", result.LaunchedAt.ToString("O"));
        sessionCommand.Parameters.AddWithValue("$endTime", (result.LaunchedAt + result.Duration).ToString("O"));
        sessionCommand.Parameters.AddWithValue("$durationSeconds", (long)result.Duration.TotalSeconds);
        sessionCommand.Parameters.AddWithValue("$exitCode", result.ExitCode is null ? DBNull.Value : result.ExitCode.Value);
        sessionCommand.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        sessionCommand.ExecuteNonQuery();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE games
            SET total_play_seconds = $totalPlaySeconds,
                last_launch_time = $lastLaunchTime
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", updated.Id);
        command.Parameters.AddWithValue("$totalPlaySeconds", (long)updated.TotalPlayTime.TotalSeconds);
        command.Parameters.AddWithValue("$lastLaunchTime", updated.LastLaunchTime?.ToString("O") ?? string.Empty);
        command.ExecuteNonQuery();
        transaction.Commit();

        return updated;
    }

    public IReadOnlyList<PlaySession> GetPlaySessions(string gameId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, game_id, machine_id, start_time, end_time, duration_seconds, exit_code, synced
            FROM play_sessions
            WHERE game_id = $gameId
            ORDER BY start_time DESC;
            """;
        command.Parameters.AddWithValue("$gameId", gameId);
        using var reader = command.ExecuteReader();
        var sessions = new List<PlaySession>();
        while (reader.Read())
        {
            sessions.Add(new PlaySession(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTime.Parse(reader.GetString(3)),
                DateTime.Parse(reader.GetString(4)),
                TimeSpan.FromSeconds(reader.GetInt64(5)),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.GetInt64(7) != 0));
        }

        return sessions;
    }

    public ExternalGameMetadata? GetExternalMetadata(string gameId)
    {
        using var connection = OpenConnection();
        return ReadExternalMetadata(connection, gameId);
    }

    public ExternalGameMetadataCloudSnapshot? GetExternalMetadataSnapshot(string gameId)
    {
        using var connection = OpenConnection();
        return ReadExternalMetadataSnapshot(connection, gameId);
    }

    public IReadOnlyList<ExternalGameMetadataCloudSnapshot> GetExternalMetadataSnapshots()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT game_id, provider, subject_id, is_linked, is_partial, original_name, localized_name, summary,
                   release_date, developer, publisher, tags_json, image_url, subject_url,
                   source_updated_at, updated_at
            FROM game_external_metadata
            ORDER BY game_id ASC;
            """;
        using var reader = command.ExecuteReader();
        var snapshots = new List<ExternalGameMetadataCloudSnapshot>();
        while (reader.Read())
        {
            var snapshot = ReadExternalMetadataSnapshot(reader);
            if (snapshot is not null)
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots;
    }

    public ExternalMetadataMergeResult ApplyCloudExternalMetadata(ExternalGameMetadataCloudSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.GameId) ||
            string.IsNullOrWhiteSpace(snapshot.Provider) ||
            string.IsNullOrWhiteSpace(snapshot.SubjectId))
        {
            return new ExternalMetadataMergeResult(
                ExternalMetadataMergeStatus.Invalid,
                "Cloud external metadata is missing game, provider, or subject information.");
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var existingGame = GetGameById(connection, snapshot.GameId, transaction);
        if (existingGame is null)
        {
            transaction.Rollback();
            return new ExternalMetadataMergeResult(
                ExternalMetadataMergeStatus.MissingGame,
                $"Game '{snapshot.GameId}' was not found.");
        }

        var existing = ReadExternalMetadataSnapshot(connection, snapshot.GameId, transaction);
        if (existing is not null &&
            (!string.Equals(existing.Provider, snapshot.Provider, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(existing.SubjectId, snapshot.SubjectId, StringComparison.OrdinalIgnoreCase)))
        {
            var reason = CreateConflictReason(snapshot.GameId, existing, snapshot);
            WriteExternalMetadataConflict(connection, transaction, new ExternalMetadataConflict(
                snapshot.GameId,
                existing,
                snapshot,
                DateTime.UtcNow,
                reason));
            transaction.Commit();
            return new ExternalMetadataMergeResult(
                ExternalMetadataMergeStatus.Conflict,
                reason);
        }

        var cloudUpdatedAt = snapshot.SnapshotUpdatedAtUtc.ToUniversalTime();
        if (existing is not null &&
            existing.SnapshotUpdatedAtUtc.ToUniversalTime() >= cloudUpdatedAt)
        {
            transaction.Rollback();
            return new ExternalMetadataMergeResult(
                ExternalMetadataMergeStatus.KeptLocal,
                $"Local external metadata for '{snapshot.GameId}' is newer.");
        }

        WriteExternalMetadata(connection, snapshot.GameId, snapshot.ToMetadata(), transaction, cloudUpdatedAt);
        DeleteExternalMetadataConflict(connection, transaction, snapshot.GameId);
        transaction.Commit();
        return new ExternalMetadataMergeResult(
            ExternalMetadataMergeStatus.Applied,
            $"Cloud external metadata for '{snapshot.GameId}' was applied.");
    }

    public ExternalMetadataConflict? GetExternalMetadataConflict(string gameId)
    {
        using var connection = OpenConnection();
        return ReadExternalMetadataConflict(connection, gameId);
    }

    public bool ClearExternalMetadataConflict(string gameId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var removed = DeleteExternalMetadataConflict(connection, transaction, gameId);
        transaction.Commit();
        return removed;
    }

    public Game ResolveExternalMetadataConflict(string gameId, ExternalMetadataConflictResolution resolution)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var conflict = ReadExternalMetadataConflict(connection, gameId, transaction)
            ?? throw new InvalidOperationException($"External metadata conflict for '{gameId}' was not found.");
        var existing = GetGameById(connection, gameId, transaction)
            ?? throw new InvalidOperationException($"Game '{gameId}' was not found.");

        var updatedAt = existing.UpdatedAtUtc;
        ExternalGameMetadata? selectedMetadata = existing.ExternalMetadata;
        if (resolution == ExternalMetadataConflictResolution.UseCloud)
        {
            selectedMetadata = conflict.CloudSnapshot.ToMetadata();
            updatedAt = conflict.CloudSnapshot.SnapshotUpdatedAtUtc.ToUniversalTime();
            WriteExternalMetadata(connection, gameId, selectedMetadata, transaction, updatedAt);
        }
        else if (resolution == ExternalMetadataConflictResolution.UnlinkLocal && existing.ExternalMetadata is not null)
        {
            selectedMetadata = existing.ExternalMetadata with { IsLinked = false };
            updatedAt = NextUpdateAt(existing.UpdatedAtUtc);
            WriteExternalMetadata(connection, gameId, selectedMetadata, transaction, updatedAt);
        }

        DeleteExternalMetadataConflict(connection, transaction, gameId);
        transaction.Commit();
        return WithExternalMetadata(existing, selectedMetadata, updatedAt);
    }

    public Game UpdateExternalMetadata(string gameId, ExternalGameMetadata? metadata)
    {
        using var connection = OpenConnection();
        var existing = GetGameById(connection, gameId)
            ?? throw new InvalidOperationException($"Game '{gameId}' was not found.");
        var updatedAt = NextUpdateAt(existing.UpdatedAtUtc);

        using var transaction = connection.BeginTransaction();
        if (metadata is null)
        {
            DeleteExternalMetadata(connection, transaction, gameId);
        }
        else
        {
            WriteExternalMetadata(connection, gameId, metadata, transaction);
        }

        DeleteExternalMetadataConflict(connection, transaction, gameId);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE games SET updated_at = $updatedAt WHERE id = $id;";
        command.Parameters.AddWithValue("$updatedAt", updatedAt.ToString("O"));
        command.Parameters.AddWithValue("$id", gameId);
        command.ExecuteNonQuery();
        transaction.Commit();

        return WithExternalMetadata(existing, metadata, updatedAt);
    }

    public BangumiCollectionState? GetBangumiCollectionState(string gameId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT game_id, subject_id, username, collection_type, rating, comment,
                   remote_updated_at, last_synced_at
            FROM bangumi_collection_states
            WHERE game_id = $gameId;
            """;
        command.Parameters.AddWithValue("$gameId", gameId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var type = Enum.TryParse<BangumiCollectionType>(reader.GetString(3), true, out var parsedType)
            ? parsedType
            : BangumiCollectionType.None;
        return new BangumiCollectionState(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            type,
            reader.GetInt32(4),
            reader.GetString(5),
            ParseUtcDate(reader.IsDBNull(6) ? null : reader.GetString(6)),
            ParseUtcDate(reader.GetString(7)) ?? DateTime.UtcNow);
    }

    public void SaveBangumiCollectionState(BangumiCollectionState state)
    {
        using var connection = OpenConnection();
        if (GetGameById(connection, state.GameId) is null)
        {
            throw new InvalidOperationException($"Game '{state.GameId}' was not found.");
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO bangumi_collection_states (
                game_id, subject_id, username, collection_type, rating, comment,
                remote_updated_at, last_synced_at
            )
            VALUES (
                $gameId, $subjectId, $username, $collectionType, $rating, $comment,
                $remoteUpdatedAt, $lastSyncedAt
            )
            ON CONFLICT(game_id) DO UPDATE SET
                subject_id = excluded.subject_id,
                username = excluded.username,
                collection_type = excluded.collection_type,
                rating = excluded.rating,
                comment = excluded.comment,
                remote_updated_at = excluded.remote_updated_at,
                last_synced_at = excluded.last_synced_at;
            """;
        command.Parameters.AddWithValue("$gameId", state.GameId);
        command.Parameters.AddWithValue("$subjectId", state.SubjectId);
        command.Parameters.AddWithValue("$username", state.Username);
        command.Parameters.AddWithValue("$collectionType", state.Type.ToString());
        command.Parameters.AddWithValue("$rating", state.Rating);
        command.Parameters.AddWithValue("$comment", state.Comment);
        command.Parameters.AddWithValue("$remoteUpdatedAt", ToDbValue(state.RemoteUpdatedAtUtc?.ToString("O")));
        command.Parameters.AddWithValue("$lastSyncedAt", state.LastSyncedAtUtc.ToUniversalTime().ToString("O"));
        command.ExecuteNonQuery();
    }

    public GameLibraryMergeResult MergeCloudMetadata(IReadOnlyList<GameCloudMetadata> metadata)
    {
        var added = 0;
        var updated = 0;
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var remote in metadata.Where(item => !string.IsNullOrWhiteSpace(item.Id)))
        {
            if (IsDeletedGame(connection, remote.Id, transaction))
            {
                continue;
            }

            var existing = GetGameById(connection, remote.Id, transaction);
            if (existing is null)
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText =
                    """
                    INSERT INTO games (
                        id, name, executable_path, game_root_path, save_path, cover_image_path,
                        total_play_seconds, last_launch_time, launch_arguments, run_as_administrator,
                        working_directory, monitor_process_name, sync_enabled, sort_order, created_at, updated_at
                    )
                    VALUES (
                        $id, $name, '', '', '', NULL, $total, $lastLaunch, '', 0,
                        '', '', 1, $sortOrder, $createdAt, $updatedAt
                    );
                    """;
                insert.Parameters.AddWithValue("$id", remote.Id);
                insert.Parameters.AddWithValue("$name", remote.Name);
                insert.Parameters.AddWithValue("$total", remote.TotalPlaySeconds);
                insert.Parameters.AddWithValue("$lastLaunch", ToDbValue(remote.LastLaunchTime?.ToString("O")));
                insert.Parameters.AddWithValue("$sortOrder", GetNextSortOrder(connection, transaction));
                insert.Parameters.AddWithValue("$createdAt", remote.UpdatedAtUtc.ToString("O"));
                insert.Parameters.AddWithValue("$updatedAt", remote.UpdatedAtUtc.ToString("O"));
                insert.ExecuteNonQuery();
                added++;
                continue;
            }

            var totalSeconds = Math.Max((long)existing.TotalPlayTime.TotalSeconds, remote.TotalPlaySeconds);
            var lastLaunch = existing.LastLaunchTime is null || remote.LastLaunchTime > existing.LastLaunchTime
                ? remote.LastLaunchTime
                : existing.LastLaunchTime;
            var localUpdatedAt = existing.UpdatedAtUtc;
            var remoteUpdatedAt = remote.UpdatedAtUtc.ToUniversalTime();
            var remoteIsNewer = remoteUpdatedAt > localUpdatedAt;
            var selectedName = remoteIsNewer ? remote.Name : existing.Name;
            var selectedUpdatedAt = remoteIsNewer
                ? remoteUpdatedAt
                : localUpdatedAt;
            if (existing.Name == selectedName &&
                (long)existing.TotalPlayTime.TotalSeconds == totalSeconds &&
                existing.LastLaunchTime == lastLaunch &&
                existing.UpdatedAtUtc == selectedUpdatedAt)
            {
                continue;
            }

            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE games
                SET name = $name,
                    total_play_seconds = $total,
                    last_launch_time = $lastLaunch,
                    updated_at = $updatedAt
                WHERE id = $id;
                """;
            update.Parameters.AddWithValue("$id", remote.Id);
            update.Parameters.AddWithValue("$name", selectedName);
            update.Parameters.AddWithValue("$total", totalSeconds);
            update.Parameters.AddWithValue("$lastLaunch", ToDbValue(lastLaunch?.ToString("O")));
            update.Parameters.AddWithValue("$updatedAt", selectedUpdatedAt.ToString("O"));
            update.ExecuteNonQuery();
            updated++;
        }

        transaction.Commit();
        return new GameLibraryMergeResult(added, updated);
    }

    public int MergePlaySessions(IReadOnlyList<PlaySession> sessions)
    {
        var added = 0;
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var session in sessions)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT OR IGNORE INTO play_sessions (
                    id, game_id, machine_id, start_time, end_time, duration_seconds, exit_code, synced, created_at
                )
                VALUES ($id, $gameId, $machineId, $startTime, $endTime, $duration, $exitCode, 1, $createdAt);
                """;
            command.Parameters.AddWithValue("$id", session.Id);
            command.Parameters.AddWithValue("$gameId", session.GameId);
            command.Parameters.AddWithValue("$machineId", session.MachineId);
            command.Parameters.AddWithValue("$startTime", session.StartedAt.ToString("O"));
            command.Parameters.AddWithValue("$endTime", session.EndedAt.ToString("O"));
            command.Parameters.AddWithValue("$duration", (long)session.Duration.TotalSeconds);
            command.Parameters.AddWithValue("$exitCode", session.ExitCode is null ? DBNull.Value : session.ExitCode.Value);
            command.Parameters.AddWithValue("$createdAt", session.EndedAt.ToString("O"));
            added += command.ExecuteNonQuery();
        }

        using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText =
            """
            UPDATE games
            SET total_play_seconds = MAX(
                    total_play_seconds,
                    (
                        SELECT COALESCE(SUM(duration_seconds), 0)
                        FROM play_sessions
                        WHERE play_sessions.game_id = games.id
                    )
                ),
                last_launch_time = MAX(
                    COALESCE(last_launch_time, ''),
                    COALESCE(
                        (
                            SELECT MAX(start_time)
                            FROM play_sessions
                            WHERE play_sessions.game_id = games.id
                        ),
                        ''
                    )
                )
            WHERE EXISTS (
                SELECT 1 FROM play_sessions WHERE play_sessions.game_id = games.id
            );
            """;
        update.ExecuteNonQuery();
        transaction.Commit();
        return added;
    }

    public void UpdateCloudCoverPath(string gameId, string? coverPath)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE games SET cover_image_path = $coverPath WHERE id = $gameId;";
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$coverPath", ToDbValue(coverPath));
        command.ExecuteNonQuery();
    }

    public void ApplyMachinePath(string gameId, MachineGamePath machinePath)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE games
            SET executable_path = CASE WHEN executable_path = '' THEN $executablePath ELSE executable_path END,
                game_root_path = CASE WHEN game_root_path = '' THEN $gameRootPath ELSE game_root_path END,
                save_path = CASE WHEN save_path = '' THEN $savePath ELSE save_path END,
                launch_arguments = CASE WHEN launch_arguments = '' THEN $launchArguments ELSE launch_arguments END,
                working_directory = CASE WHEN working_directory = '' THEN $workingDirectory ELSE working_directory END,
                monitor_process_name = CASE WHEN monitor_process_name = '' THEN $monitorProcessName ELSE monitor_process_name END,
                run_as_administrator = CASE WHEN executable_path = '' AND $runAsAdministrator IS NOT NULL THEN $runAsAdministrator ELSE run_as_administrator END,
                sync_enabled = CASE WHEN executable_path = '' AND $syncEnabled IS NOT NULL THEN $syncEnabled ELSE sync_enabled END
            WHERE id = $gameId;
            """;
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$executablePath", machinePath.ExecutablePath ?? string.Empty);
        command.Parameters.AddWithValue("$gameRootPath", machinePath.GameRootPath ?? string.Empty);
        command.Parameters.AddWithValue("$savePath", machinePath.SavePath ?? string.Empty);
        command.Parameters.AddWithValue("$launchArguments", machinePath.LaunchArguments ?? string.Empty);
        command.Parameters.AddWithValue("$workingDirectory", machinePath.WorkingDirectory ?? string.Empty);
        command.Parameters.AddWithValue("$monitorProcessName", machinePath.MonitorProcessName ?? string.Empty);
        command.Parameters.AddWithValue("$runAsAdministrator", machinePath.RunAsAdministrator is null ? DBNull.Value : machinePath.RunAsAdministrator.Value ? 1 : 0);
        command.Parameters.AddWithValue("$syncEnabled", machinePath.SyncEnabled is null ? DBNull.Value : machinePath.SyncEnabled.Value ? 1 : 0);
        command.ExecuteNonQuery();
    }

    private void InitializeDatabase()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS games (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                executable_path TEXT NOT NULL,
                game_root_path TEXT NOT NULL,
                save_path TEXT NOT NULL,
                cover_image_path TEXT,
                total_play_seconds INTEGER NOT NULL DEFAULT 0,
                last_launch_time TEXT,
                launch_arguments TEXT NOT NULL DEFAULT '',
                run_as_administrator INTEGER NOT NULL DEFAULT 0,
                working_directory TEXT NOT NULL DEFAULT '',
                monitor_process_name TEXT NOT NULL DEFAULT '',
                sync_enabled INTEGER NOT NULL DEFAULT 1,
                sort_order INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_games_sort_order ON games(sort_order);

            CREATE TABLE IF NOT EXISTS play_sessions (
                id TEXT PRIMARY KEY,
                game_id TEXT NOT NULL,
                machine_id TEXT NOT NULL,
                start_time TEXT NOT NULL,
                end_time TEXT NOT NULL,
                duration_seconds INTEGER NOT NULL,
                exit_code INTEGER,
                synced INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_play_sessions_game_id ON play_sessions(game_id);

            CREATE TABLE IF NOT EXISTS sync_records (
                id TEXT PRIMARY KEY,
                game_id TEXT,
                sync_type TEXT NOT NULL,
                direction TEXT NOT NULL,
                status TEXT NOT NULL,
                started_at TEXT NOT NULL,
                finished_at TEXT,
                message TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS save_sync_states (
                game_id TEXT PRIMARY KEY,
                local_hash TEXT NOT NULL DEFAULT '',
                remote_hash TEXT NOT NULL DEFAULT '',
                last_synced_hash TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT 'not-synced',
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS migration_records (
                migration_id TEXT PRIMARY KEY,
                applied_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS deleted_games (
                id TEXT PRIMARY KEY,
                deleted_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS game_external_metadata (
                game_id TEXT PRIMARY KEY,
                provider TEXT NOT NULL,
                subject_id TEXT NOT NULL,
                is_linked INTEGER NOT NULL DEFAULT 1,
                is_partial INTEGER NOT NULL DEFAULT 0,
                original_name TEXT NOT NULL DEFAULT '',
                localized_name TEXT NOT NULL DEFAULT '',
                summary TEXT NOT NULL DEFAULT '',
                release_date TEXT NOT NULL DEFAULT '',
                developer TEXT NOT NULL DEFAULT '',
                publisher TEXT NOT NULL DEFAULT '',
                tags_json TEXT NOT NULL DEFAULT '[]',
                image_url TEXT NOT NULL DEFAULT '',
                subject_url TEXT NOT NULL DEFAULT '',
                source_updated_at TEXT NOT NULL,
                imported_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_game_external_metadata_provider_subject
            ON game_external_metadata(provider, subject_id);

            CREATE TABLE IF NOT EXISTS bangumi_collection_states (
                game_id TEXT PRIMARY KEY,
                subject_id TEXT NOT NULL,
                username TEXT NOT NULL,
                collection_type TEXT NOT NULL DEFAULT 'None',
                rating INTEGER NOT NULL DEFAULT 0,
                comment TEXT NOT NULL DEFAULT '',
                remote_updated_at TEXT,
                last_synced_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS external_metadata_conflicts (
                game_id TEXT PRIMARY KEY,
                local_snapshot_json TEXT NOT NULL,
                cloud_snapshot_json TEXT NOT NULL,
                detected_at TEXT NOT NULL,
                reason TEXT NOT NULL DEFAULT ''
            );
            """;
        command.ExecuteNonQuery();
        EnsureColumn(connection, "launch_arguments", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "run_as_administrator", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "working_directory", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "monitor_process_name", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "sync_enabled", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumn(connection, "game_external_metadata", "is_partial", "INTEGER NOT NULL DEFAULT 0");
        RepairDegradedExternalMetadataLinks(connection);
        SeedLegacyPlaySessions(connection);
    }

    private static void RepairDegradedExternalMetadataLinks(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE game_external_metadata
            SET is_linked = 1,
                subject_url = 'https://bgm.tv/subject/' || trim(subject_id)
            WHERE lower(trim(provider)) = 'bangumi'
              AND trim(subject_id) <> ''
              AND trim(subject_url) = '';
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static Game ReadGame(SqliteDataReader reader)
    {
        var lastLaunchText = reader.IsDBNull(7) ? null : reader.GetString(7);
        var lastLaunchTime = DateTime.TryParse(lastLaunchText, out var parsedLastLaunchTime)
            ? parsedLastLaunchTime
            : (DateTime?)null;
        var updatedAtUtc = !reader.IsDBNull(13) && DateTimeOffset.TryParse(reader.GetString(13), out var parsedUpdatedAt)
            ? parsedUpdatedAt.UtcDateTime
            : DateTime.UtcNow;

        return new Game(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            TimeSpan.FromSeconds(reader.GetInt64(6)),
            lastLaunchTime,
            reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
            !reader.IsDBNull(9) && reader.GetInt64(9) != 0,
            reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
            reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
            reader.IsDBNull(12) || reader.GetInt64(12) != 0,
            updatedAtUtc);
    }

    private static void AddGameParameters(SqliteCommand command, Game game)
    {
        command.Parameters.AddWithValue("$id", game.Id);
        command.Parameters.AddWithValue("$name", game.Name);
        command.Parameters.AddWithValue("$executablePath", game.ExecutablePath);
        command.Parameters.AddWithValue("$gameRootPath", game.GameRootPath);
        command.Parameters.AddWithValue("$savePath", game.SavePath);
        command.Parameters.AddWithValue("$coverImagePath", ToDbValue(game.CoverImagePath));
        command.Parameters.AddWithValue("$totalPlaySeconds", (long)game.TotalPlayTime.TotalSeconds);
        command.Parameters.AddWithValue("$lastLaunchTime", ToDbValue(game.LastLaunchTime?.ToString("O")));
        command.Parameters.AddWithValue("$launchArguments", game.LaunchArguments);
        command.Parameters.AddWithValue("$runAsAdministrator", game.RunAsAdministrator ? 1 : 0);
        command.Parameters.AddWithValue("$workingDirectory", game.WorkingDirectory);
        command.Parameters.AddWithValue("$monitorProcessName", game.MonitorProcessName);
        command.Parameters.AddWithValue("$syncEnabled", game.SyncEnabled ? 1 : 0);
    }

    private static ExternalGameMetadata? ReadExternalMetadata(
        SqliteConnection connection,
        string gameId,
        SqliteTransaction? transaction = null)
    {
        return ReadExternalMetadataSnapshot(connection, gameId, transaction)?.ToMetadata();
    }

    private static ExternalGameMetadataCloudSnapshot? ReadExternalMetadataSnapshot(
        SqliteConnection connection,
        string gameId,
        SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT game_id, provider, subject_id, is_linked, is_partial, original_name, localized_name, summary,
                   release_date, developer, publisher, tags_json, image_url, subject_url,
                   source_updated_at, updated_at
            FROM game_external_metadata
            WHERE game_id = $gameId;
            """;
        command.Parameters.AddWithValue("$gameId", gameId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadExternalMetadataSnapshot(reader) : null;
    }

    private static ExternalGameMetadataCloudSnapshot? ReadExternalMetadataSnapshot(SqliteDataReader reader)
    {
        try
        {
            var metadata = new ExternalGameMetadata
            {
                Provider = reader.GetString(1),
                SubjectId = reader.GetString(2),
                IsLinked = reader.GetInt64(3) != 0,
                IsPartial = reader.GetInt64(4) != 0,
                OriginalName = reader.GetString(5),
                LocalizedName = reader.GetString(6),
                Summary = reader.GetString(7),
                ReleaseDate = reader.GetString(8),
                Developer = reader.GetString(9),
                Publisher = reader.GetString(10),
                Tags = JsonSerializer.Deserialize<List<string>>(reader.GetString(11)) ?? [],
                ImageUrl = reader.GetString(12),
                SubjectUrl = reader.GetString(13),
                SourceUpdatedAtUtc = ParseUtcDate(reader.GetString(14)) ?? DateTime.MinValue
            };
            return ExternalGameMetadataCloudSnapshot.FromMetadata(
                reader.GetString(0),
                metadata,
                ParseUtcDate(reader.GetString(15)) ?? metadata.SourceUpdatedAtUtc);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidCastException)
        {
            return null;
        }
    }

    private static void WriteExternalMetadata(
        SqliteConnection connection,
        string gameId,
        ExternalGameMetadata metadata,
        SqliteTransaction? transaction = null,
        DateTime? snapshotUpdatedAtUtc = null)
    {
        var now = DateTime.UtcNow;
        var nowText = now.ToString("O");
        var updatedAt = (snapshotUpdatedAtUtc ?? now).ToUniversalTime().ToString("O");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO game_external_metadata (
                game_id, provider, subject_id, is_linked, is_partial, original_name, localized_name, summary,
                release_date, developer, publisher, tags_json, image_url, subject_url,
                source_updated_at, imported_at, updated_at
            )
            VALUES (
                $gameId, $provider, $subjectId, $isLinked, $isPartial, $originalName, $localizedName, $summary,
                $releaseDate, $developer, $publisher, $tagsJson, $imageUrl, $subjectUrl,
                $sourceUpdatedAt, $importedAt, $updatedAt
            )
            ON CONFLICT(game_id) DO UPDATE SET
                provider = excluded.provider,
                subject_id = excluded.subject_id,
                is_linked = excluded.is_linked,
                is_partial = excluded.is_partial,
                original_name = excluded.original_name,
                localized_name = excluded.localized_name,
                summary = excluded.summary,
                release_date = excluded.release_date,
                developer = excluded.developer,
                publisher = excluded.publisher,
                tags_json = excluded.tags_json,
                image_url = excluded.image_url,
                subject_url = excluded.subject_url,
                source_updated_at = excluded.source_updated_at,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$provider", metadata.Provider);
        command.Parameters.AddWithValue("$subjectId", metadata.SubjectId);
        command.Parameters.AddWithValue("$isLinked", metadata.IsLinked ? 1 : 0);
        command.Parameters.AddWithValue("$isPartial", metadata.IsPartial ? 1 : 0);
        command.Parameters.AddWithValue("$originalName", metadata.OriginalName);
        command.Parameters.AddWithValue("$localizedName", metadata.LocalizedName);
        command.Parameters.AddWithValue("$summary", metadata.Summary);
        command.Parameters.AddWithValue("$releaseDate", metadata.ReleaseDate);
        command.Parameters.AddWithValue("$developer", metadata.Developer);
        command.Parameters.AddWithValue("$publisher", metadata.Publisher);
        command.Parameters.AddWithValue("$tagsJson", JsonSerializer.Serialize(metadata.Tags));
        command.Parameters.AddWithValue("$imageUrl", metadata.ImageUrl);
        command.Parameters.AddWithValue("$subjectUrl", metadata.SubjectUrl);
        command.Parameters.AddWithValue("$sourceUpdatedAt", metadata.SourceUpdatedAtUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$importedAt", nowText);
        command.Parameters.AddWithValue("$updatedAt", updatedAt);
        command.ExecuteNonQuery();
    }

    private static void DeleteExternalMetadata(SqliteConnection connection, SqliteTransaction transaction, string gameId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM game_external_metadata WHERE game_id = $gameId;";
        command.Parameters.AddWithValue("$gameId", gameId);
        command.ExecuteNonQuery();
    }

    private static ExternalMetadataConflict? ReadExternalMetadataConflict(
        SqliteConnection connection,
        string gameId,
        SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT game_id, local_snapshot_json, cloud_snapshot_json, detected_at, reason
            FROM external_metadata_conflicts
            WHERE game_id = $gameId;
            """;
        command.Parameters.AddWithValue("$gameId", gameId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        try
        {
            var local = JsonSerializer.Deserialize<ExternalGameMetadataCloudSnapshot>(reader.GetString(1));
            var cloud = JsonSerializer.Deserialize<ExternalGameMetadataCloudSnapshot>(reader.GetString(2));
            if (local is null || cloud is null)
            {
                return null;
            }

            return new ExternalMetadataConflict(
                reader.GetString(0),
                local,
                cloud,
                ParseUtcDate(reader.GetString(3)) ?? DateTime.UtcNow,
                reader.GetString(4));
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidCastException)
        {
            return null;
        }
    }

    private static void WriteExternalMetadataConflict(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ExternalMetadataConflict conflict)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO external_metadata_conflicts (
                game_id, local_snapshot_json, cloud_snapshot_json, detected_at, reason
            )
            VALUES (
                $gameId, $localSnapshotJson, $cloudSnapshotJson, $detectedAt, $reason
            )
            ON CONFLICT(game_id) DO UPDATE SET
                local_snapshot_json = excluded.local_snapshot_json,
                cloud_snapshot_json = excluded.cloud_snapshot_json,
                detected_at = excluded.detected_at,
                reason = excluded.reason;
            """;
        command.Parameters.AddWithValue("$gameId", conflict.GameId);
        command.Parameters.AddWithValue("$localSnapshotJson", JsonSerializer.Serialize(conflict.LocalSnapshot));
        command.Parameters.AddWithValue("$cloudSnapshotJson", JsonSerializer.Serialize(conflict.CloudSnapshot));
        command.Parameters.AddWithValue("$detectedAt", conflict.DetectedAtUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$reason", conflict.Reason);
        command.ExecuteNonQuery();
    }

    private static bool DeleteExternalMetadataConflict(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string gameId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM external_metadata_conflicts WHERE game_id = $gameId;";
        command.Parameters.AddWithValue("$gameId", gameId);
        return command.ExecuteNonQuery() > 0;
    }

    private static void DeleteRelatedGameData(SqliteConnection connection, SqliteTransaction transaction, string gameId)
    {
        foreach (var tableName in new[] { "game_external_metadata", "bangumi_collection_states", "external_metadata_conflicts" })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DELETE FROM {tableName} WHERE game_id = $gameId;";
            command.Parameters.AddWithValue("$gameId", gameId);
            command.ExecuteNonQuery();
        }
    }

    private static Game WithExternalMetadata(Game game, ExternalGameMetadata? metadata, DateTime? updatedAtUtc = null)
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
            updatedAtUtc ?? game.UpdatedAtUtc,
            metadata);
    }

    private static string CreateConflictReason(
        string gameId,
        ExternalGameMetadataCloudSnapshot local,
        ExternalGameMetadataCloudSnapshot cloud)
    {
        return $"External metadata conflict for '{gameId}': local {local.Provider}/{local.SubjectId}, cloud {cloud.Provider}/{cloud.SubjectId}.";
    }

    private static DateTime? ParseUtcDate(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed.UtcDateTime : null;
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
            left.IsPartial == right.IsPartial &&
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

    private static object ToDbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }

    private static long GetNextSortOrder(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM games;";
        return (long)command.ExecuteScalar()!;
    }

    private static Game? GetGameById(SqliteConnection connection, string id, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id,
                   name,
                   executable_path,
                   game_root_path,
                   save_path,
                   cover_image_path,
                   total_play_seconds,
                   last_launch_time,
                   launch_arguments,
                   run_as_administrator,
                   working_directory,
                   monitor_process_name,
                   sync_enabled,
                   updated_at
            FROM games
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);

        Game? game;
        using (var reader = command.ExecuteReader())
        {
            game = reader.Read() ? ReadGame(reader) : null;
        }

        return game is null
            ? null
            : WithExternalMetadata(game, ReadExternalMetadata(connection, id, transaction));
    }

    private static bool IsDeletedGame(SqliteConnection connection, string id, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM deleted_games WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return (long)command.ExecuteScalar()! > 0;
    }

    private static List<string> GetOrderedIds(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM games ORDER BY sort_order ASC, created_at ASC;";
        using var reader = command.ExecuteReader();
        var ids = new List<string>();
        while (reader.Read())
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    private static void ReindexSortOrder(SqliteConnection connection, SqliteTransaction transaction)
    {
        UpdateSortOrder(connection, transaction, GetOrderedIds(connection));
    }

    private static void UpdateSortOrder(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<string> orderedIds)
    {
        for (var i = 0; i < orderedIds.Count; i++)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE games SET sort_order = $sortOrder WHERE id = $id;";
            command.Parameters.AddWithValue("$sortOrder", i);
            command.Parameters.AddWithValue("$id", orderedIds[i]);
            command.ExecuteNonQuery();
        }
    }

    private static void EnsureColumn(SqliteConnection connection, string columnName, string definition)
    {
        EnsureColumn(connection, "games", columnName, definition);
    }

    private static void EnsureColumn(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string definition)
    {
        using var check = connection.CreateCommand();
        check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name = $name;";
        check.Parameters.AddWithValue("$name", columnName);
        if ((long)check.ExecuteScalar()! > 0)
        {
            return;
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};";
        alter.ExecuteNonQuery();
    }

    private void BackupLegacyDatabaseIfNeeded()
    {
        if (!File.Exists(databasePath) || File.Exists(databasePath + ".pre-v2.bak"))
        {
            return;
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'play_sessions';";
        var alreadyV2 = (long)command.ExecuteScalar()! > 0;
        connection.Close();
        if (!alreadyV2)
        {
            File.Copy(databasePath, databasePath + ".pre-v2.bak", false);
        }
    }

    private static void SeedLegacyPlaySessions(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO play_sessions (
                id, game_id, machine_id, start_time, end_time, duration_seconds,
                exit_code, synced, created_at
            )
            SELECT
                'legacy-' || id,
                id,
                'legacy',
                COALESCE(last_launch_time, created_at),
                COALESCE(last_launch_time, updated_at),
                total_play_seconds,
                NULL,
                1,
                updated_at
            FROM games
            WHERE total_play_seconds > 0
              AND NOT EXISTS (
                  SELECT 1
                  FROM migration_records
                  WHERE migration_id = 'v2-play-sessions'
              )
              AND NOT EXISTS (
                  SELECT 1 FROM play_sessions WHERE play_sessions.game_id = games.id
              );

            INSERT OR IGNORE INTO migration_records (migration_id, applied_at)
            VALUES ('v2-play-sessions', $appliedAt);
            """;
        command.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }
}
