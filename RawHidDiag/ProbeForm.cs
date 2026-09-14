using System.Runtime.InteropServices;

namespace RawHidDiag;

public class ProbeForm : Form
{
    private nint _preparsedData;
    private bool _ready;
    private StreamWriter? _log;

    private const ushort DigitizerPage = 0x0D;
    private const ushort GenericDesktopPage = 0x01;
    private const ushort UsageContactCount = 0x54;
    private const ushort UsageX = 0x30;
    private const ushort UsageY = 0x31;

    public ProbeForm()
    {
        Text = "Raw HID Probe (minimize me, watch the console)";
        Width = 400;
        Height = 100;
        Load += (_, _) => RegisterForTouchpad();
    }

    private void Log(string line)
    {
        Console.WriteLine(line);
        _log ??= new StreamWriter(Path.Combine(AppContext.BaseDirectory, "probe-log.txt"), append: false) { AutoFlush = true };
        _log.WriteLine(line);
    }

    private void RegisterForTouchpad()
    {
        var devices = new[]
        {
            new RAWINPUTDEVICE
            {
                usUsagePage = 0x0D,
                usUsage = 0x05,
                dwFlags = Native.RIDEV_INPUTSINK,
                hwndTarget = Handle
            }
        };

        bool ok = Native.RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
        Log(ok
            ? "Registered for raw digitizer (touchpad) input. Now: rest one finger, then try 2/3/4-finger swipes."
            : $"RegisterRawInputDevices FAILED, error {Marshal.GetLastWin32Error()}");
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_INPUT)
        {
            HandleInput(m.LParam);
        }
        base.WndProc(ref m);
    }

    private void HandleInput(nint hRawInput)
    {
        uint size = 0;
        uint headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
        Native.GetRawInputData(hRawInput, Native.RID_INPUT, 0, ref size, headerSize);
        if (size == 0) return;

        nint buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            uint written = Native.GetRawInputData(hRawInput, Native.RID_INPUT, buffer, ref size, headerSize);
            if (written != size) return;

            var header = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);
            if (header.dwType != Native.RIM_TYPEHID) return;

            nint dataPtr = buffer + (int)headerSize;
            uint dwSizeHid = (uint)Marshal.ReadInt32(dataPtr, 0);
            uint dwCount = (uint)Marshal.ReadInt32(dataPtr, 4);
            nint rawDataPtr = dataPtr + 8;

            if (!_ready)
            {
                _ready = true;
                SetUpParsing(header.hDevice);
            }
            if (_preparsedData == 0) return;

            for (int i = 0; i < dwCount; i++)
            {
                var report = new byte[dwSizeHid];
                Marshal.Copy(rawDataPtr + (int)(i * dwSizeHid), report, 0, (int)dwSizeHid);
                DecodeAndPrint(report);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void DecodeAndPrint(byte[] report)
    {
        Native.HidP_GetUsageValue(Native.HidP_Input, DigitizerPage, 0, UsageContactCount,
            out uint contactCount, _preparsedData, report, (uint)report.Length);

        var ys = new List<string>();
        for (ushort link = 1; link <= 5; link++)
        {
            int rcX = Native.HidP_GetUsageValue(Native.HidP_Input, GenericDesktopPage, link, UsageX,
                out uint x, _preparsedData, report, (uint)report.Length);
            int rcY = Native.HidP_GetUsageValue(Native.HidP_Input, GenericDesktopPage, link, UsageY,
                out uint y, _preparsedData, report, (uint)report.Length);
            if (rcX == Native.HIDP_STATUS_SUCCESS && rcY == Native.HIDP_STATUS_SUCCESS)
                ys.Add($"f{link}=({x},{y})");
        }

        Log($"{DateTime.Now:HH:mm:ss.fff} contactCount={contactCount}  {string.Join(" ", ys)}");
    }

    private static string? GetDeviceName(nint hDevice)
    {
        uint size = 0;
        Native.GetRawInputDeviceInfo(hDevice, Native.RIDI_DEVICENAME, 0, ref size);
        if (size == 0) return null;

        nint buf = Marshal.AllocHGlobal(((int)size + 4) * 2);
        try
        {
            uint size2 = size + 4;
            Native.GetRawInputDeviceInfo(hDevice, Native.RIDI_DEVICENAME, buf, ref size2);
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
        if (string.IsNullOrEmpty(path))
        {
            Log("Could not resolve device path.");
            return;
        }

        nint handle = Native.CreateFile(path, 0, Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
            0, Native.OPEN_EXISTING, 0, 0);
        if (handle == -1 || handle == 0)
        {
            Log($"CreateFile FAILED, error {Marshal.GetLastWin32Error()}.");
            return;
        }

        try
        {
            if (!Native.HidD_GetPreparsedData(handle, out _preparsedData))
            {
                Log("HidD_GetPreparsedData FAILED.");
                _preparsedData = 0;
            }
            else
            {
                Log("Parsing ready.\n");
            }
        }
        finally
        {
            Native.CloseHandle(handle); // preparsed data blob stays valid after closing the handle
        }
    }
}
