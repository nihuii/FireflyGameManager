using System.IO;

namespace GameManager.App.Services;

public sealed class MachineIdentityService
{
    private readonly string path;

    public MachineIdentityService(string path)
    {
        this.path = path;
    }

    public string GetOrCreate()
    {
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path).Trim();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }
        }

        var id = $"{Environment.MachineName}-{Guid.NewGuid():N}";
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, id);
        return id;
    }
}
