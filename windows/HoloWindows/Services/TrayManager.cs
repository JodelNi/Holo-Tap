using Forms = System.Windows.Forms;

namespace Holo.Services;

/// <summary>
/// Windows system tray manager.
/// Maps to macOS menu bar behavior using NotifyIcon (WinForms interop).
/// </summary>
public sealed class TrayManager : IDisposable
{
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly Forms.ContextMenuStrip _trayMenu;

    public event Action? StartRecordingClicked;
    public event Action? StopRecordingClicked;
    public event Action? ShowWindowClicked;
    public event Action? QuitClicked;

    public TrayManager()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "Holo — Desk Tap Detection",
            Visible = true
        };

        _trayMenu = new Forms.ContextMenuStrip();
        _trayMenu.Items.Add("Start Recording", null, (_, _) => StartRecordingClicked?.Invoke());
        _trayMenu.Items.Add("Stop Recording", null, (_, _) => StopRecordingClicked?.Invoke());
        _trayMenu.Items.Add(new Forms.ToolStripSeparator());
        _trayMenu.Items.Add("Show Window", null, (_, _) => ShowWindowClicked?.Invoke());
        _trayMenu.Items.Add(new Forms.ToolStripSeparator());
        _trayMenu.Items.Add("Quit", null, (_, _) => QuitClicked?.Invoke());

        _trayIcon.ContextMenuStrip = _trayMenu;
        _trayIcon.DoubleClick += (_, _) => ShowWindowClicked?.Invoke();
    }

    public void SetRecordingState(bool isRecording)
    {
        _trayIcon.Text = isRecording
            ? "Holo — Listening for taps..."
            : "Holo — Desk Tap Detection";
    }

    public void ShowNotification(string title, string text)
    {
        _trayIcon.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);
    }

    /// <summary>
    /// Creates a simple 16x16 icon programmatically.
    /// TODO: Replace with a proper .ico resource file.
    /// </summary>
    private static System.Drawing.Icon CreateDefaultIcon()
    {
                    using var bmp = new System.Drawing.Bitmap(16, 16);
                    using var g = System.Drawing.Graphics.FromImage(bmp);
                    g.Clear(System.Drawing.Color.Transparent);

                    using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 120, 215));
                    g.FillRectangle(brush, 2, 2, 3, 12);
                    g.FillRectangle(brush, 11, 2, 3, 12);
                    g.FillRectangle(brush, 2, 5, 12, 3);

                    return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayMenu.Dispose();
    }
}
