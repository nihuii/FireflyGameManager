namespace GameManager.App.Models;

public sealed class WebDavDownloadResult
{
    public WebDavDownloadResult(bool success, string message, int downloadedCount, int failedCount)
    {
        Success = success;
        Message = message;
        DownloadedCount = downloadedCount;
        FailedCount = failedCount;
    }

    public bool Success { get; }

    public string Message { get; }

    public int DownloadedCount { get; }

    public int FailedCount { get; }
}
