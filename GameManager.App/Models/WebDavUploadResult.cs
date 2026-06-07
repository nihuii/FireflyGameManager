namespace GameManager.App.Models;

public sealed class WebDavUploadResult
{
    public WebDavUploadResult(bool success, string message, int uploadedCount, int failedCount)
    {
        Success = success;
        Message = message;
        UploadedCount = uploadedCount;
        FailedCount = failedCount;
    }

    public bool Success { get; }

    public string Message { get; }

    public int UploadedCount { get; }

    public int FailedCount { get; }
}
