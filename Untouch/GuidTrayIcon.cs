using System.Runtime.InteropServices;

namespace Untouch;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NOTIFYICONDATA
{
    public int cbSize;
    public nint hWnd;
    public uint uID;
    public uint uFlags;
    public uint uCallbackMessage;
    public nint hIcon;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string szTip;
    public uint dwState;
    public uint dwStateMask;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string szInfo;
    public uint uVersionOrTimeout;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string szInfoTitle;
    public uint dwInfoFlags;
    public Guid guidItem;
    public nint hBalloonIcon;
}

// A system tray icon with a fixed GUID identity, so Windows' "always show in tray"
// preference stays attached to this app permanently -- not to its current exe path,
// which is what the default NotifyIcon (and Explorer's own heuristic) relies on, and
// which resets every time the exe is moved, renamed, or rebuilt at a new location.
internal sealed class GuidTrayIcon : Form
{
    private const uint NIM_ADD = 0x0;
    private const uint NIM_MODIFY = 0x1;
    private const uint NIM_DELETE = 0x2;

    private const uint NIF_MESSAGE = 0x1;
    private const uint NIF_ICON = 0x2;
    private const uint NIF_TIP = 0x4;
    private const uint NIF_GUID = 0x20;
    private const uint NIF_SHOWTIP = 0x80;

    private const int WM_TRAYCALLBACK = 0x8001; // WM_APP-range, arbitrary but fixed
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_CONTEXTMENU = 0x007B;

    // Fixed forever: this is what makes the tray-visibility preference stick across
    // renames/rebuilds/relocations. Never change this once users have set their preference.
    private static readonly Guid IconGuid = new("6f2a2a8e-2f77-4b7a-9a5d-2a6a9f9f6b5b");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    public ContextMenuStrip? TrayMenu { get; set; }
    public event Action? TrayDoubleClick;

    private Icon _icon = SystemIcons.Application;
    private string _tip = "";
    private bool _added;

    public GuidTrayIcon()
    {
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Opacity = 0;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
    }

    public void Show(Icon icon, string tip)
    {
        _icon = icon;
        _tip = tip;
        _ = Handle; // force window creation before we reference it

        // Defensively remove any stale icon left behind by a previous crashed instance.
        var stale = MakeData(NIF_GUID);
        Shell_NotifyIcon(NIM_DELETE, ref stale);

        var data = MakeData(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP | NIF_GUID);
        Shell_NotifyIcon(NIM_ADD, ref data);
        _added = true;
    }

    public void UpdateIcon(Icon icon)
    {
        _icon = icon;
        if (!_added) return;
        var data = MakeData(NIF_ICON | NIF_GUID);
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    public void UpdateTip(string tip)
    {
        _tip = tip;
        if (!_added) return;
        var data = MakeData(NIF_TIP | NIF_SHOWTIP | NIF_GUID);
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    private NOTIFYICONDATA MakeData(uint flags) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = Handle,
        uCallbackMessage = WM_TRAYCALLBACK,
        hIcon = _icon.Handle,
        szTip = _tip,
        guidItem = IconGuid,
        uFlags = flags
    };

    protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_TRAYCALLBACK)
        {
            int mouseMsg = (int)(m.LParam.ToInt64() & 0xFFFF);
            if (mouseMsg == WM_LBUTTONDBLCLK)
            {
                TrayDoubleClick?.Invoke();
            }
            else if (mouseMsg == WM_RBUTTONUP || mouseMsg == WM_CONTEXTMENU)
            {
                if (TrayMenu != null)
                {
                    SetForegroundWindow(Handle);
                    TrayMenu.Show(Cursor.Position);
                }
            }
        }
        base.WndProc(ref m);
    }

    public void Remove()
    {
        if (!_added) return;
        var data = MakeData(NIF_GUID);
        Shell_NotifyIcon(NIM_DELETE, ref data);
        _added = false;
    }
}
