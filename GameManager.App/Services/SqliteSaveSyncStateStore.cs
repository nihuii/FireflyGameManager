using GameManager.App.Models;
using Microsoft.Data.Sqlite;

namespace GameManager.App.Services;

public sealed class SqliteSaveSyncStateStore : ISaveSyncStateStore
{
    private readonly string connectionString;

    public SqliteSaveSyncStateStore(string databasePath)
    {
        _ = new SqliteGameLibraryService(databasePath);
        connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString();
    }

    public SaveSyncState? Get(string gameId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT game_id, local_hash, remote_hash, last_synced_hash, status, updated_at
            FROM save_sync_states
            WHERE game_id = $gameId;
            """;
        command.Parameters.AddWithValue("$gameId", gameId);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new SaveSyncState(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                DateTime.Parse(reader.GetString(5)))
            : null;
    }

    public void Save(SaveSyncState state)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO save_sync_states (
                game_id, local_hash, remote_hash, last_synced_hash, status, updated_at
            )
            VALUES ($gameId, $localHash, $remoteHash, $lastSyncedHash, $status, $updatedAt)
            ON CONFLICT(game_id) DO UPDATE SET
                local_hash = excluded.local_hash,
                remote_hash = excluded.remote_hash,
                last_synced_hash = excluded.last_synced_hash,
                status = excluded.status,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$gameId", state.GameId);
        command.Parameters.AddWithValue("$localHash", state.LocalHash);
        command.Parameters.AddWithValue("$remoteHash", state.RemoteHash);
        command.Parameters.AddWithValue("$lastSyncedHash", state.LastSyncedHash);
        command.Parameters.AddWithValue("$status", state.Status);
        command.Parameters.AddWithValue("$updatedAt", state.UpdatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
}
