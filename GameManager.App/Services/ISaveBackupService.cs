using GameManager.App.Models;

namespace GameManager.App.Services;

public interface ISaveBackupService
{
    Task<string> BackupAsync(Game game);

    Task RestoreAsync(Game game, string backupPath);

    string GetBackupDirectory(Game game);

    IReadOnlyList<SaveBackupEntry> GetBackups(Game game);

    bool DeleteBackup(string backupPath);
}
