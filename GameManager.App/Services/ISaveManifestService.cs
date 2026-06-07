using GameManager.App.Models;

namespace GameManager.App.Services;

public interface ISaveManifestService
{
    SaveManifest Create(string saveDirectory);

    SaveManifest CreateFromArchive(string archivePath);
}
