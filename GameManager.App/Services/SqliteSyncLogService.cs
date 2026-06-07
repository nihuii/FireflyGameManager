using GameManager.App.Models;
using Microsoft.Data.Sqlite;

namespace GameManager.App.Services;

public sealed class SqliteSyncLogService : ISyncLogService
{
    private readonly string connectionString;

    public SqliteSyncLogService(string databasePath)
    {
        _ = new SqliteGameLibraryService(databasePath);
        connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString();
    }

    public void Add(string? gameId, string syncType, string direction, string status, string message)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO sync_records (
                id, game_id, sync_type, direction, status, started_at, finished_at, message
            )
            VALUES ($id, $gameId, $syncType, $direction, $status, $startedAt, $finishedAt, $message);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("$gameId", string.IsNullOrWhiteSpace(gameId) ? DBNull.Value : gameId);
        command.Parameters.AddWithValue("$syncType", syncType);
        command.Parameters.AddWithValue("$direction", direction);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$startedAt", now);
        command.Parameters.AddWithValue("$finishedAt", now);
        command.Parameters.AddWithValue("$message", message);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<SyncRecord> GetRecent(int count = 50)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, game_id, sync_type, direction, status, started_at, finished_at, message
            FROM sync_records
            ORDER BY started_at DESC
            LIMIT $count;
            """;
        command.Parameters.AddWithValue("$count", Math.Max(1, count));
        using var reader = command.ExecuteReader();
        var records = new List<SyncRecord>();
        while (reader.Read())
        {
            records.Add(new SyncRecord(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                DateTime.Parse(reader.GetString(5)),
                reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
                reader.GetString(7)));
        }

        return records;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
}
