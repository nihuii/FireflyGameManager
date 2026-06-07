namespace GameManager.App.Services;

public sealed class NoopAutoStartService : IAutoStartService
{
    private bool enabled;

    public bool IsEnabled()
    {
        return enabled;
    }

    public void SetEnabled(bool enabled)
    {
        this.enabled = enabled;
    }
}
