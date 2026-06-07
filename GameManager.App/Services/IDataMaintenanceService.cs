namespace GameManager.App.Services;

public interface IDataMaintenanceService
{
    string DataDirectory { get; }

    void OpenDataDirectory();

    void Export(string destinationZipPath);

    void Import(string sourceZipPath);

    int ClearInvalidBackups(IEnumerable<string> validGameIds);

    void ClearCoverCache();
}
