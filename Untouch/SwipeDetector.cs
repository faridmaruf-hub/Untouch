using System.Runtime.InteropServices;

namespace Untouch;

internal enum SwipeDirection { Up, Down, Left, Right }

// Watches the touchpad's raw digitizer (multi-touch) reports for 4-finger swipes in any
// direction. Runs independently of MouseSuppressor so it keeps seeing input even while
// pointer output is fully blocked.
internal sealed class SwipeDetector : Form
{
    private const ushort DigitizerPage = 0x0D;
    private const ushort GenericDesktopPage = 0x01;
    private const ushort UsageContactCount = 0x54;
    private const ushort UsageX = 0x30;
    private const ushort UsageY = 0x31;

    private const int Threshold = 400;      // device units; observed real swipes move ~1700
    private const int MaxWindowMs = 900;
    private const int TargetFingers = 4;

    private nint _preparsedData;
    private bool _setupAttempted;

    private bool _tracking;
    private double _baselineX, _baselineY;
    private DateTime _baselineTime;
    private bool _cooldown; // waiting for release before re-arming after a fire

    public event Action<SwipeDirection>? SwipeDetected;

    public SwipeDetector()
    {
        // Never shown; exists only to own a window handle for WM_INPUT delivery.
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Opacity = 0;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
    }

    protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

    // Forces the native window handle to exist, then registers for raw input on it.
    // Must be called once before any input can be received.
    public void Initialize()
    {
        _ = Handle;
        Register();
    }

    private void Register()
    {
        var devices = new[]
        {
            new RAWINPUTDEVICE
            {
                usUsagePage = 0x0D,
                usUsage = 0x05, // Touch Pad
                dwFlags = NativeMethods.RIDEV_INPUTSINK,
                hwndTarget = Handle
            }
        };
        NativeMethods.RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_INPUT)
        {
            HandleInput(m.LParam);
        }
        base.WndProc(ref m);
    }

    private void HandleInput(nint hRawInput)
    {
        uint size = 0;
        uint headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
        NativeMethods.GetRawInputData(hRawInput, NativeMethods.RID_INPUT, 0, ref size, headerSize);
        if (size == 0) return;

        nint buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            uint written = NativeMethods.GetRawInputData(hRawInput, NativeMethods.RID_INPUT, buffer, ref size, headerSize);
            if (written != size) return;

            var header = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);
            if (header.dwType != NativeMethods.RIM_TYPEHID) return;

            if (!_setupAttempted)
            {
                _setupAttempted = true;
                SetUpParsing(header.hDevice);
            }
            if (_preparsedData == 0) return;

            nint dataPtr = buffer + (int)headerSize;
            uint dwSizeHid = (uint)Marshal.ReadInt32(dataPtr, 0);
            uint dwCount = (uint)Marshal.ReadInt32(dataPtr, 4);
            nint rawDataPtr = dataPtr + 8;

            for (int i = 0; i < dwCount; i++)
            {
                var report = new byte[dwSizeHid];
                Marshal.Copy(rawDataPtr + (int)(i * dwSizeHid), report, 0, (int)dwSizeHid);
                ProcessReport(report);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void ProcessReport(byte[] report)
    {
        NativeMethods.HidP_GetUsageValue(NativeMethods.HidP_Input, DigitizerPage, 0, UsageContactCount,
            out uint contactCount, _preparsedData, report, (uint)report.Length);

        if (contactCount != TargetFingers)
        {
            _tracking = false;
            _cooldown = false; // re-arm as soon as fingers stop being at exactly 4
            return;
        }

        if (_cooldown) return; // already fired for this continuous touch-down

        double sumX = 0, sumY = 0;
        int found = 0;
        for (ushort link = 1; link <= TargetFingers; link++)
        {
            int rcX = NativeMethods.HidP_GetUsageValue(NativeMethods.HidP_Input, GenericDesktopPage, link, UsageX,
                out uint x, _preparsedData, report, (uint)report.Length);
            int rcY = NativeMethods.HidP_GetUsageValue(NativeMethods.HidP_Input, GenericDesktopPage, link, UsageY,
                out uint y, _preparsedData, report, (uint)report.Length);
            if (rcX == NativeMethods.HIDP_STATUS_SUCCESS && rcY == NativeMethods.HIDP_STATUS_SUCCESS)
            {
                sumX += x;
                sumY += y;
                found++;
            }
        }
        if (found == 0) return;
        double centroidX = sumX / found;
        double centroidY = sumY / found;

        var now = DateTime.UtcNow;
        if (!_tracking)
        {
            _tracking = true;
            _baselineX = centroidX;
            _baselineY = centroidY;
            _baselineTime = now;
            return;
        }

        if ((now - _baselineTime).TotalMilliseconds > MaxWindowMs)
        {
            // Slide the window forward so a slow continuous swipe can still be caught.
            _baselineX = centroidX;
            _baselineY = centroidY;
            _baselineTime = now;
            return;
        }

        double deltaX = centroidX - _baselineX;
        double deltaY = centroidY - _baselineY;

        if (Math.Max(Math.Abs(deltaX), Math.Abs(deltaY)) <= Threshold) return;

        SwipeDirection direction;
        if (Math.Abs(deltaY) >= Math.Abs(deltaX))
            direction = deltaY < 0 ? SwipeDirection.Up : SwipeDirection.Down;
        else
            direction = deltaX < 0 ? SwipeDirection.Right : SwipeDirection.Left;

        _cooldown = true;
        _tracking = false;
        SwipeDetected?.Invoke(direction);
    }

    private static string? GetDeviceName(nint hDevice)
    {
        uint size = 0;
        NativeMethods.GetRawInputDeviceInfo(hDevice, NativeMethods.RIDI_DEVICENAME, 0, ref size);
        if (size == 0) return null;

        nint buf = Marshal.AllocHGlobal(((int)size + 4) * 2);
        try
        {
            uint size2 = size + 4;
            NativeMethods.GetRawInputDeviceInfo(hDevice, NativeMethods.RIDI_DEVICENAME, buf, ref size2);
            Marshal.WriteInt16(buf, ((int)size + 3) * 2, 0);
            return Marshal.PtrToStringUni(buf);
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private void SetUpParsing(nint hDevice)
    {
        var path = GetDeviceName(hDevice);
        if (string.IsNullOrEmpty(path)) return;

        nint handle = NativeMethods.CreateFile(path, 0, NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            0, NativeMethods.OPEN_EXISTING, 0, 0);
        if (handle == -1 || handle == 0) return;

        try
        {
            if (!NativeMethods.HidD_GetPreparsedData(handle, out _preparsedData))
            {
                _preparsedData = 0;
            }
        }
        finally
        {
            NativeMethods.CloseHandle(handle); // preparsed data blob stays valid after closing the handle
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _preparsedData != 0)
        {
            NativeMethods.HidD_FreePreparsedData(_preparsedData);
            _preparsedData = 0;
        }
        base.Dispose(disposing);
    }
}
