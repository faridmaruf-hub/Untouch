namespace Untouch;

// Blocks cursor movement and clicks (single-finger drift/taps) while Blocked is true -- but
// only while the touchpad is actually being touched (or was a moment ago), so an external
// mouse keeps working normally whenever the touchpad is idle. WH_MOUSE_LL doesn't say which
// device an event came from, so "touchpad recently reported a contact" (from SwipeDetector's
// raw digitizer stream) stands in for "this event came from the touchpad."
// Two-finger scroll is always left working: Windows converts it to WM_MOUSEWHEEL/
// WM_MOUSEHWHEEL without moving the cursor, so it's a distinct message type we can exempt.
internal sealed class MouseSuppressor : IDisposable
{
    private nint _hookHandle;
    private readonly NativeMethods.LowLevelHookProc _proc;

    // How long after the last touchpad contact report pointer events are still treated as
    // coming from the touchpad. Covers tap-to-click (the click is synthesized after the finger
    // lifts) and the small gap before the first raw report of a new touch is processed.
    private const long TouchGraceMs = 300;

    private readonly Func<long> _lastTouchpadContactTick;

    public bool Blocked { get; set; }

    public MouseSuppressor(Func<long> lastTouchpadContactTick)
    {
        _proc = HookCallback;
        _lastTouchpadContactTick = lastTouchpadContactTick;
    }

    private bool TouchpadActive =>
        Environment.TickCount64 - _lastTouchpadContactTick() <= TouchGraceMs;

    public void Install()
    {
        using var curModule = System.Diagnostics.Process.GetCurrentProcess().MainModule!;
        var hMod = NativeMethods.GetModuleHandle(curModule.ModuleName);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, hMod, 0);
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        bool isWheel = wParam == NativeMethods.WM_MOUSEWHEEL || wParam == NativeMethods.WM_MOUSEHWHEEL;
        if (nCode >= 0 && Blocked && !isWheel && TouchpadActive)
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
