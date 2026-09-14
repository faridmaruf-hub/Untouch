namespace Untouch;

// Ctrl+Alt+Shift+F9 always forces the mouse unblocked, regardless of scheme state.
// Deliberately independent of MouseSuppressor: keyboard and mouse low-level hooks
// are separate subsystems, so this keeps working even if mouse input is fully blocked.
internal sealed class EmergencyHotkey : IDisposable
{
    private const int VK_F9 = 0x78;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12; // Alt
    private const int VK_SHIFT = 0x10;

    private nint _hookHandle;
    private readonly NativeMethods.LowLevelHookProc _proc;

    public event Action? Triggered;

    public EmergencyHotkey()
    {
        _proc = HookCallback;
    }

    public void Install()
    {
        using var curModule = System.Diagnostics.Process.GetCurrentProcess().MainModule!;
        var hMod = NativeMethods.GetModuleHandle(curModule.ModuleName);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, hMod, 0);
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && (wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN))
        {
            var data = System.Runtime.InteropServices.Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            if (data.vkCode == VK_F9
                && (NativeMethods.GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0
                && (NativeMethods.GetAsyncKeyState(VK_MENU) & 0x8000) != 0
                && (NativeMethods.GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0)
            {
                Triggered?.Invoke();
            }
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
