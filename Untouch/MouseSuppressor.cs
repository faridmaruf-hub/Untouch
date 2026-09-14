namespace Untouch;

// Blocks cursor movement and clicks (single-finger drift/taps) system-wide while Blocked is
// true, for any pointer device -- internal touchpad or an external mouse alike. Two-finger
// scroll is deliberately left working: Windows converts it to WM_MOUSEWHEEL/WM_MOUSEHWHEEL
// without moving the cursor, so it's a distinct message type we can exempt rather than a
// per-device thing we'd need to correlate.
internal sealed class MouseSuppressor : IDisposable
{
    private nint _hookHandle;
    private readonly NativeMethods.LowLevelHookProc _proc;

    public bool Blocked { get; set; }

    public MouseSuppressor()
    {
        _proc = HookCallback;
    }

    public void Install()
    {
        using var curModule = System.Diagnostics.Process.GetCurrentProcess().MainModule!;
        var hMod = NativeMethods.GetModuleHandle(curModule.ModuleName);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, hMod, 0);
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        bool isWheel = wParam == NativeMethods.WM_MOUSEWHEEL || wParam == NativeMethods.WM_MOUSEHWHEEL;
        if (nCode >= 0 && Blocked && !isWheel)
        {
            return 1; // swallow the event
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookHandle != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = 0;
        }
    }
}
