namespace GameManager.App.Services;

public interface IRemoteImageCacheService
{
    Task<string?> DownloadAsync(
        string provider,
        string subjectId,
        string imageUrl,
        CancellationToken cancellationToken = default);
}
