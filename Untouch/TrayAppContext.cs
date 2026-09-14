using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Untouch;

internal sealed class TrayAppContext : ApplicationContext
{
    private const string TaskName = "Untouch";

    private readonly GuidTrayIcon _trayIcon = new();
    private readonly MouseSuppressor _suppressor = new();
    private readonly SwipeDetector _swipeDetector = new();
    private readonly EmergencyHotkey _emergencyHotkey = new();
    private readonly Config _config;

    private readonly ToolStripMenuItem _schemeItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _statusItem;

    private readonly Icon _iconOn = MakeTouchpadIcon(Color.SeaGreen);
    private readonly Icon _iconOff = MakeTouchpadIcon(Color.Gray);

    public TrayAppContext()
    {
        _config = Config.Load();

        var menu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem("Touchpad: on") { Enabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());

        _schemeItem = new ToolStripMenuItem("Four-finger-swipe toggle enabled", null, (_, _) => ToggleScheme())
        {
            Checked = _config.SchemeEnabled
        };
        menu.Items.Add(_schemeItem);

        menu.Items.Add(new ToolStripMenuItem("Force enable (safety)", null, (_, _) => ForceEnable()));
        menu.Items.Add(new ToolStripMenuItem("  (or press Ctrl+Alt+Shift+F9 anytime)") { Enabled = false });

        _startupItem = new ToolStripMenuItem("Start automatically at login", null, (_, _) => ToggleStartup())
        {
            Checked = IsStartupTaskInstalled()
        };
        menu.Items.Add(_startupItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitApp()));

        _trayIcon.TrayMenu = menu;
        _trayIcon.TrayDoubleClick += ToggleScheme;
        _trayIcon.Show(_iconOn, "Untouch");

        _swipeDetector.SwipeDetected += OnSwipeDetected;
        _swipeDetector.Initialize();
        _suppressor.Install();
        _emergencyHotkey.Triggered += () => SetBlocked(false);
        _emergencyHotkey.Install();

        ApplyInitialState();

        Application.ApplicationExit += (_, _) => Cleanup();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => Cleanup();
    }

    private void ApplyInitialState()
    {
        // Always start with the touchpad enabled, regardless of the scheme setting;
        // blocking only kicks in after an explicit swipe.
        SetBlocked(false);
    }

    private void OnSwipeDetected(SwipeDirection direction)
    {
        if (!_config.SchemeEnabled) return;

        switch (direction)
        {
            case SwipeDirection.Up:
                SetBlocked(!_suppressor.Blocked);
                break;
            case SwipeDirection.Down:
                KeySender.SendCombo(KeySender.VK_LWIN, KeySender.VK_D);
                break;
            case SwipeDirection.Left:
                KeySender.SendCombo(KeySender.VK_LCONTROL, KeySender.VK_LWIN, KeySender.VK_LEFT);
                break;
            case SwipeDirection.Right:
                KeySender.SendCombo(KeySender.VK_LCONTROL, KeySender.VK_LWIN, KeySender.VK_RIGHT);
                break;
        }
    }

    private void SetBlocked(bool blocked)
    {
        _suppressor.Blocked = blocked;
        _statusItem.Text = blocked ? "Touchpad: off (swipe 4 fingers up to enable)" : "Touchpad: on";
        _trayIcon.UpdateIcon(blocked ? _iconOff : _iconOn);
        _trayIcon.UpdateTip(blocked ? "Untouch: touchpad OFF" : "Untouch: touchpad ON");
    }

    private void ToggleScheme()
    {
        _config.SchemeEnabled = !_config.SchemeEnabled;
        _config.Save();
        _schemeItem.Checked = _config.SchemeEnabled;

        // Resting state: blocked while the scheme is active (until a swipe happens), always on while paused.
        SetBlocked(_config.SchemeEnabled);
    }

    private void ForceEnable()
    {
        SetBlocked(false);
    }

    private static bool IsStartupTaskInstalled()
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks", $"/Query /TN \"{TaskName}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private void ToggleStartup()
    {
        try
        {
            if (_startupItem.Checked)
            {
                Process.Start(new ProcessStartInfo("schtasks", $"/Delete /TN \"{TaskName}\" /F")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                })!.WaitForExit();
                _startupItem.Checked = false;
            }
            else
            {
                var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                Process.Start(new ProcessStartInfo("schtasks",
                    $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\"\" /SC ONLOGON /F")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                })!.WaitForExit();
                _startupItem.Checked = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not update startup task: {ex.Message}", "Untouch",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static Icon MakeTouchpadIcon(Color color)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Trackpad body: rounded rectangle with a thin click-bar near the bottom,
            // so it reads as "touchpad" rather than a generic colored blob.
            using var body = RoundedRect(new Rectangle(2, 2, 28, 24), 5);
            using var fill = new SolidBrush(color);
            g.FillPath(fill, body);
            using var border = new Pen(Color.FromArgb(160, Color.Black), 1.5f);
            g.DrawPath(border, body);

            using var clickBar = new Pen(Color.FromArgb(160, Color.Black), 1.5f);
            g.DrawLine(clickBar, 6, 20, 26, 20);
        }
        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void ExitApp()
    {
        Cleanup();
        Application.Exit();
    }

    private void Cleanup()
    {
        _suppressor.Blocked = false; // never leave input stuck blocked
        _suppressor.Dispose();
        _swipeDetector.Dispose();
        _emergencyHotkey.Dispose();
        _trayIcon.Remove();
        _trayIcon.Dispose();
        _iconOn.Dispose();
        _iconOff.Dispose();
    }
}
