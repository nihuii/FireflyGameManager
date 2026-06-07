using Microsoft.Win32;

namespace GameManager.App.Services;

public sealed class RegistryAutoStartService : IAutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string applicationName;
    private readonly string executablePath;

    public RegistryAutoStartService(string applicationName, string executablePath)
    {
        this.applicationName = applicationName;
        this.executablePath = executablePath;
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(applicationName) is string value
            && value.Contains(executablePath, StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
        {
            key.SetValue(applicationName, $"\"{executablePath}\" --minimized");
        }
        else
        {
            key.DeleteValue(applicationName, false);
        }
    }
}
