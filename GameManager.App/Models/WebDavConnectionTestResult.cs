namespace GameManager.App.Models;

public sealed class WebDavConnectionTestResult
{
    public WebDavConnectionTestResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public bool Success { get; }

    public string Message { get; }
}
