using System.Windows;

namespace GameManager.App.Services;

public sealed class WpfGameSessionPresentationService : IGameSessionPresentationService
{
    public void Minimize()
    {
        RunOnUiThread(window =>
        {
            window.WindowState = WindowState.Minimized;
        });
    }

    public void Restore()
    {
        RunOnUiThread(window =>
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        });
    }

    private static void RunOnUiThread(Action<Window> action)
    {
        var application = Application.Current;
        if (application?.MainWindow is not Window window)
        {
            return;
        }

        application.Dispatcher.Invoke(() => action(window));
    }
}
