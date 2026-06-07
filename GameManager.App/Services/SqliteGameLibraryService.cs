using System.IO;
using GameManager.App.Models;
using Microsoft.Data.Sqlite;

namespace GameManager.App.Services;

public sealed class SqliteGameLibraryService : IGameLibraryService
{
    private readonly string databasePath;
    private readonly string connectionString;

    public SqliteGameLibraryService(string databasePath)
    {
        this.databasePath = databasePath;
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
                   run_as_administrator
            FROM games
            ORDER BY sort_order ASC, created_at ASC;
            """;

        using var reader = command.ExecuteReader();
        var games = new List<Game>();
        while (reader.Read())
        {
            games.Add(ReadGame(reader));
        }

        return games;
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
            request.RunAsAdministrator);
        var now = DateTimeOffset.UtcNow.ToString("O");

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
                $sortOrder,
                $createdAt,
                $updatedAt
            );
            """;
        AddGameParameters(command, game);
        command.Parameters.AddWithValue("$sortOrder", GetNextSortOrder(connection));
        command.Parameters.AddWithValue("$createdAt", now);
        command.Parameters.AddWithValue("$updatedAt", now);
        command.ExecuteNonQuery();

        return game;
    }

    public bool DeleteGame(string id)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM games WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        var affectedRows = command.ExecuteNonQuery();
        if (affectedRows > 0)
        {
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
            request.RunAsAdministrator);

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
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();

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
            existing.RunAsAdministrator);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE games
            SET total_play_seconds = $totalPlaySeconds,
                last_launch_time = $lastLaunchTime,
                updated_at = $updatedAt
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", updated.Id);
        command.Parameters.AddWithValue("$totalPlaySeconds", (long)updated.TotalPlayTime.TotalSeconds);
        command.Parameters.AddWithValue("$lastLaunchTime", updated.LastLaunchTime?.ToString("O") ?? string.Empty);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();

        return updated;
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
                sort_order INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_games_sort_order ON games(sort_order);
            """;
        command.ExecuteNonQuery();
        EnsureColumn(connection, "launch_arguments", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "run_as_administrator", "INTEGER NOT NULL DEFAULT 0");
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
            !reader.IsDBNull(9) && reader.GetInt64(9) != 0);
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
    }

    private static object ToDbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }

    private static long GetNextSortOrder(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM games;";
        return (long)command.ExecuteScalar()!;
    }

    private static Game? GetGameById(SqliteConnection connection, string id)
    {
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
                   run_as_administrator
            FROM games
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadGame(reader) : null;
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
            command.CommandText = "UPDATE games SET sort_order = $sortOrder, updated_at = $updatedAt WHERE id = $id;";
            command.Parameters.AddWithValue("$sortOrder", i);
            command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$id", orderedIds[i]);
            command.ExecuteNonQuery();
        }
    }

    private static void EnsureColumn(SqliteConnection connection, string columnName, string definition)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('games') WHERE name = $name;";
        check.Parameters.AddWithValue("$name", columnName);
        if ((long)check.ExecuteScalar()! > 0)
        {
            return;
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE games ADD COLUMN {columnName} {definition};";
        alter.ExecuteNonQuery();
    }
}
