using GameManager.App.Models;

namespace GameManager.App.Services;

public interface IWebDavConnectionTester
{
    Task<WebDavConnectionTestResult> TestConnectionAsync(WebDavSettings settings);
}
