namespace Untouch;

// Blocks ALL mouse/touchpad-driven pointer input system-wide while Blocked is true.
// We don't try to distinguish the touchpad from an external mouse: with no modifier key
// held during clicks, there's no ergonomic reason not to block everything uniformly, and
// it avoids the fragility of per-device correlation.
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
        if (nCode >= 0 && Blocked)
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
