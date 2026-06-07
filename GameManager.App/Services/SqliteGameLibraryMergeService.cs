using System.IO;
using GameManager.App.Models;
using Microsoft.Data.Sqlite;

namespace GameManager.App.Services;

public sealed class SqliteGameLibraryMergeService
{
    public GameLibraryMergeResult MergeRemoteIntoLocal(string localDatabasePath, string remoteDatabasePath)
    {
        _ = new SqliteGameLibraryService(localDatabasePath);
        if (!File.Exists(remoteDatabasePath))
        {
            return new GameLibraryMergeResult(0, 0);
        }

        _ = new SqliteGameLibraryService(remoteDatabasePath);
        var localRows = ReadRows(localDatabasePath).ToDictionary(row => row.Id, StringComparer.OrdinalIgnoreCase);
        var remoteRows = ReadRows(remoteDatabasePath);
        var remotePlaySessions = ReadPlaySessions(remoteDatabasePath);
        var localDeletedIds = ReadDeletedIds(localDatabasePath);
        var remoteDeletedGames = ReadDeletedGames(remoteDatabasePath);
        var deletedIds = new HashSet<string>(localDeletedIds, StringComparer.OrdinalIgnoreCase);
        deletedIds.UnionWith(remoteDeletedGames.Select(game => game.Id));
        var nextSortOrder = localRows.Count == 0 ? 0 : localRows.Values.Max(row => row.SortOrder) + 1;
        var addedCount = 0;
        var updatedCount = 0;

        using var connection = OpenConnection(localDatabasePath);
        using var transaction = connection.BeginTransaction();
        ApplyRemoteDeletions(connection, transaction, remoteDeletedGames);
        foreach (var remoteRow in remoteRows)
        {
            if (deletedIds.Contains(remoteRow.Id))
            {
                continue;
            }

            if (!localRows.TryGetValue(remoteRow.Id, out var localRow))
            {
                InsertRow(connection, transaction, remoteRow with
                {
                    ExecutablePath = string.Empty,
                    GameRootPath = string.Empty,
                    SavePath = string.Empty,
                    CoverImagePath = null,
                    SortOrder = nextSortOrder++
                });
                addedCount++;
                continue;
            }

            var selectedInfo = IsRemoteNewer(remoteRow, localRow) ? remoteRow : localRow;
            var mergedRow = selectedInfo with
            {
                ExecutablePath = localRow.ExecutablePath,
                GameRootPath = localRow.GameRootPath,
                SavePath = localRow.SavePath,
                CoverImagePath = localRow.CoverImagePath,
                CreatedAt = localRow.CreatedAt,
                SortOrder = localRow.SortOrder,
                TotalPlaySeconds = Math.Max(localRow.TotalPlaySeconds, remoteRow.TotalPlaySeconds),
                LastLaunchTime = MaxTimestamp(localRow.LastLaunchTime, remoteRow.LastLaunchTime),
                UpdatedAt = MaxTimestamp(localRow.UpdatedAt, remoteRow.UpdatedAt)
            };

            if (!RowsEqual(localRow, mergedRow))
            {
                UpdateRow(connection, transaction, mergedRow);
                updatedCount++;
            }
        }

        MergeRemotePlaySessions(connection, transaction, remotePlaySessions, deletedIds);
        transaction.Commit();
        return new GameLibraryMergeResult(addedCount, updatedCount);
    }

    private static IReadOnlyList<GameRow> ReadRows(string databasePath)
    {
        using var connection = OpenConnection(databasePath);
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
                   sort_order,
                   created_at,
                   updated_at
            FROM games
            ORDER BY sort_order ASC, created_at ASC;
            """;

        using var reader = command.ExecuteReader();
        var rows = new List<GameRow>();
        while (reader.Read())
        {
            rows.Add(new GameRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetInt64(6),
                reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                reader.GetInt64(8),
                reader.GetString(9),
                reader.GetString(10)));
        }

        return rows;
    }

    private static HashSet<string> ReadDeletedIds(string databasePath)
    {
        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM deleted_games;";
        using var reader = command.ExecuteReader();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    private static IReadOnlyList<DeletedGameRow> ReadDeletedGames(string databasePath)
    {
        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, deleted_at FROM deleted_games;";
        using var reader = command.ExecuteReader();
        var rows = new List<DeletedGameRow>();
        while (reader.Read())
        {
            rows.Add(new DeletedGameRow(reader.GetString(0), reader.GetString(1)));
        }

        return rows;
    }

    private static IReadOnlyList<PlaySession> ReadPlaySessions(string databasePath)
    {
        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, game_id, machine_id, start_time, end_time, duration_seconds, exit_code, synced
            FROM play_sessions;
            """;
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

    private static void ApplyRemoteDeletions(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<DeletedGameRow> remoteDeletedGames)
    {
        foreach (var deletedGame in remoteDeletedGames)
        {
            using var tombstone = connection.CreateCommand();
            tombstone.Transaction = transaction;
            tombstone.CommandText =
                """
                INSERT INTO deleted_games (id, deleted_at)
                VALUES ($id, $deletedAt)
                ON CONFLICT(id) DO UPDATE SET deleted_at = MAX(deleted_at, excluded.deleted_at);
                """;
            tombstone.Parameters.AddWithValue("$id", deletedGame.Id);
            tombstone.Parameters.AddWithValue("$deletedAt", deletedGame.DeletedAt);
            tombstone.ExecuteNonQuery();

            using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM games WHERE id = $id;";
            delete.Parameters.AddWithValue("$id", deletedGame.Id);
            delete.ExecuteNonQuery();
        }
    }

    private static void MergeRemotePlaySessions(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<PlaySession> remotePlaySessions,
        ISet<string> deletedGameIds)
    {
        foreach (var session in remotePlaySessions.Where(session => !deletedGameIds.Contains(session.GameId)))
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT OR IGNORE INTO play_sessions (
                    id, game_id, machine_id, start_time, end_time, duration_seconds, exit_code, synced, created_at
                )
                VALUES (
                    $id, $gameId, $machineId, $startTime, $endTime, $durationSeconds, $exitCode, $synced, $createdAt
                );
                """;
            insert.Parameters.AddWithValue("$id", session.Id);
            insert.Parameters.AddWithValue("$gameId", session.GameId);
            insert.Parameters.AddWithValue("$machineId", session.MachineId);
            insert.Parameters.AddWithValue("$startTime", session.StartedAt.ToString("O"));
            insert.Parameters.AddWithValue("$endTime", session.EndedAt.ToString("O"));
            insert.Parameters.AddWithValue("$durationSeconds", (long)session.Duration.TotalSeconds);
            insert.Parameters.AddWithValue("$exitCode", session.ExitCode is null ? DBNull.Value : session.ExitCode.Value);
            insert.Parameters.AddWithValue("$synced", session.Synced ? 1 : 0);
            insert.Parameters.AddWithValue("$createdAt", session.EndedAt.ToString("O"));
            insert.ExecuteNonQuery();
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
    }

    private static void InsertRow(SqliteConnection connection, SqliteTransaction transaction, GameRow row)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
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
                $sortOrder,
                $createdAt,
                $updatedAt
            );
            """;
        AddParameters(command, row);
        command.ExecuteNonQuery();
    }

    private static void UpdateRow(SqliteConnection connection, SqliteTransaction transaction, GameRow row)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE games
            SET name = $name,
                executable_path = $executablePath,
                game_root_path = $gameRootPath,
                save_path = $savePath,
                cover_image_path = $coverImagePath,
                total_play_seconds = $totalPlaySeconds,
                last_launch_time = $lastLaunchTime,
                sort_order = $sortOrder,
                created_at = $createdAt,
                updated_at = $updatedAt
            WHERE id = $id;
            """;
        AddParameters(command, row);
        command.ExecuteNonQuery();
    }

    private static void AddParameters(SqliteCommand command, GameRow row)
    {
        command.Parameters.AddWithValue("$id", row.Id);
        command.Parameters.AddWithValue("$name", row.Name);
        command.Parameters.AddWithValue("$executablePath", row.ExecutablePath);
        command.Parameters.AddWithValue("$gameRootPath", row.GameRootPath);
        command.Parameters.AddWithValue("$savePath", row.SavePath);
        command.Parameters.AddWithValue("$coverImagePath", string.IsNullOrWhiteSpace(row.CoverImagePath) ? DBNull.Value : row.CoverImagePath);
        command.Parameters.AddWithValue("$totalPlaySeconds", row.TotalPlaySeconds);
        command.Parameters.AddWithValue("$lastLaunchTime", string.IsNullOrWhiteSpace(row.LastLaunchTime) ? DBNull.Value : row.LastLaunchTime);
        command.Parameters.AddWithValue("$sortOrder", row.SortOrder);
        command.Parameters.AddWithValue("$createdAt", row.CreatedAt);
        command.Parameters.AddWithValue("$updatedAt", row.UpdatedAt);
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    private static bool IsRemoteNewer(GameRow remoteRow, GameRow localRow)
    {
        return CompareTimestamps(remoteRow.UpdatedAt, localRow.UpdatedAt) > 0;
    }

    private static string MaxTimestamp(string first, string second)
    {
        return CompareTimestamps(first, second) >= 0 ? first : second;
    }

    private static int CompareTimestamps(string first, string second)
    {
        var hasFirst = DateTimeOffset.TryParse(first, out var firstTime);
        var hasSecond = DateTimeOffset.TryParse(second, out var secondTime);
        if (hasFirst && hasSecond)
        {
            return firstTime.CompareTo(secondTime);
        }

        if (hasFirst)
        {
            return 1;
        }

        if (hasSecond)
        {
            return -1;
        }

        return string.Compare(first, second, StringComparison.Ordinal);
    }

    private static bool RowsEqual(GameRow first, GameRow second)
    {
        return first.Id == second.Id &&
            first.Name == second.Name &&
            first.ExecutablePath == second.ExecutablePath &&
            first.GameRootPath == second.GameRootPath &&
            first.SavePath == second.SavePath &&
            first.CoverImagePath == second.CoverImagePath &&
            first.TotalPlaySeconds == second.TotalPlaySeconds &&
            first.LastLaunchTime == second.LastLaunchTime &&
            first.SortOrder == second.SortOrder &&
            first.CreatedAt == second.CreatedAt &&
            first.UpdatedAt == second.UpdatedAt;
    }

    private sealed record GameRow(
        string Id,
        string Name,
        string ExecutablePath,
        string GameRootPath,
        string SavePath,
        string? CoverImagePath,
        long TotalPlaySeconds,
        string LastLaunchTime,
        long SortOrder,
        string CreatedAt,
        string UpdatedAt);

    private sealed record DeletedGameRow(string Id, string DeletedAt);
}
