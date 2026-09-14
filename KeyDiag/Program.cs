using System.Runtime.InteropServices;

Console.WriteLine("Key diagnostic running. Press keys (including Fn combos) to see what Windows reports.");
Console.WriteLine("Press Ctrl+C in this console window to quit.\n");

nint hookHandle = 0;
LowLevelKeyboardProc proc = HookCallback; // keep a reference alive so the delegate isn't GC'd
hookHandle = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, proc, Native.GetModuleHandle(null), 0);
if (hookHandle == 0)
{
    Console.WriteLine("Failed to install hook.");
    return;
}

while (Native.GetMessage(out var msg, 0, 0, 0) > 0)
{
    Native.TranslateMessage(ref msg);
    Native.DispatchMessage(ref msg);
}

Native.UnhookWindowsHookEx(hookHandle);

nint HookCallback(int nCode, nint wParam, nint lParam)
{
    if (nCode >= 0)
    {
        var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        string action = wParam switch
        {
            0x100 => "KEYDOWN",
            0x101 => "KEYUP",
            0x104 => "SYSKEYDOWN",
            0x105 => "SYSKEYUP",
            _ => $"WM_{wParam:X}"
        };
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  {action,-10} vkCode=0x{data.vkCode:X2}  scanCode=0x{data.scanCode:X3}  flags=0x{data.flags:X2}");
    }
    return Native.CallNextHookEx(hookHandle, nCode, wParam, lParam);
}

delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

[StructLayout(LayoutKind.Sequential)]
struct KBDLLHOOKSTRUCT
{
    public uint vkCode;
    public uint scanCode;
    public uint flags;
    public uint time;
    public nint dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
struct MSG
{
    public nint hwnd;
    public uint message;
    public nint wParam;
    public nint lParam;
    public uint time;
    public int ptX;
    public int ptY;
}

static class Native
{
    public const int WH_KEYBOARD_LL = 13;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    public static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    public static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    public static extern int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    public static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern nint DispatchMessage(ref MSG lpMsg);
}
