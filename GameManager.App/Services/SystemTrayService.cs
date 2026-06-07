using System.IO;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace GameManager.App.Services;

public sealed class SystemTrayService : IDisposable
{
    private readonly Forms.NotifyIcon notifyIcon;
    private readonly Drawing.Icon? customIcon;

    public SystemTrayService(Action openWindow, Action exitApplication, string? iconPath = null)
    {
        customIcon = LoadIcon(iconPath);

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开 Firefly", null, (_, _) => openWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exitApplication());

        notifyIcon = new Forms.NotifyIcon
        {
            Text = "Firefly Game Manager",
            Icon = customIcon ?? Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        notifyIcon.DoubleClick += (_, _) => openWindow();
    }

    public void ShowNotification(string title, string message)
    {
        notifyIcon.BalloonTipTitle = title;
        notifyIcon.BalloonTipText = message;
        notifyIcon.ShowBalloonTip(2500);
    }

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        customIcon?.Dispose();
    }

    private static Drawing.Icon? LoadIcon(string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
        {
            return null;
        }

        try
        {
            return new Drawing.Icon(iconPath!);
        }
        catch
        {
            return null;
        }
    }
}
