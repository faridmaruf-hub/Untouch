using System.Runtime.InteropServices;

namespace RawHidDiag;

[StructLayout(LayoutKind.Sequential)]
struct RAWINPUTDEVICE
{
    public ushort usUsagePage;
    public ushort usUsage;
    public uint dwFlags;
    public nint hwndTarget;
}

[StructLayout(LayoutKind.Sequential)]
struct RAWINPUTHEADER
{
    public uint dwType;
    public uint dwSize;
    public nint hDevice;
    public nint wParam;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct HIDP_CAPS
{
    public ushort Usage;
    public ushort UsagePage;
    public ushort InputReportByteLength;
    public ushort OutputReportByteLength;
    public ushort FeatureReportByteLength;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
    public ushort[] Reserved;
    public ushort NumberLinkCollectionNodes;
    public ushort NumberInputButtonCaps;
    public ushort NumberInputValueCaps;
    public ushort NumberInputDataIndices;
    public ushort NumberOutputButtonCaps;
    public ushort NumberOutputValueCaps;
    public ushort NumberOutputDataIndices;
    public ushort NumberFeatureButtonCaps;
    public ushort NumberFeatureValueCaps;
    public ushort NumberFeatureDataIndices;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct HIDP_VALUE_CAPS
{
    public ushort UsagePage;
    public byte ReportID;
    public byte IsAlias;
    public ushort BitField;
    public ushort LinkCollection;
    public ushort LinkUsage;
    public ushort LinkUsagePage;
    public byte IsRange;
    public byte IsStringRange;
    public byte IsDesignatorRange;
    public byte IsAbsolute;
    public byte HasNull;
    public byte Reserved;
    public ushort BitSize;
    public ushort ReportCount;
    public ushort R2_0, R2_1, R2_2, R2_3, R2_4;
    public uint UnitsExp;
    public uint Units;
    public int LogicalMin, LogicalMax;
    public int PhysicalMin, PhysicalMax;
    // Union of Range{UsageMin,UsageMax,...} / NotRange{Usage,Reserved1,...} -- first ushort is
    // UsageMin when IsRange, else Usage. Second is UsageMax or Reserved1. That's all we need.
    public ushort UsageOrMin;
    public ushort UsageMaxOrReserved;
    public ushort U2, U3, U4, U5, U6, U7;
}

static class Native
{
    public const int WM_INPUT = 0x00FF;
    public const uint RIDEV_INPUTSINK = 0x00000100;
    public const uint RID_INPUT = 0x10000003;
    public const uint RIM_TYPEHID = 2;
    public const uint RIDI_DEVICENAME = 0x20000007;
    public const int HidP_Input = 0;
    public const int HIDP_STATUS_SUCCESS = 0x00110000;

    [DllImport("kernel32.dll")]
    public static extern bool AllocConsole();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll")]
    public static extern uint GetRawInputData(nint hRawInput, uint uiCommand, nint pData, ref uint pcbSize, uint cbSizeHeader);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetRawInputDeviceInfoW")]
    public static extern uint GetRawInputDeviceInfo(nint hDevice, uint uiCommand, nint pData, ref uint pcbSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern nint CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        nint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, nint hTemplateFile);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(nint hObject);

    [DllImport("hid.dll")]
    public static extern bool HidD_GetPreparsedData(nint hidDeviceObject, out nint preparsedData);

    [DllImport("hid.dll")]
    public static extern bool HidD_FreePreparsedData(nint preparsedData);

    [DllImport("hid.dll")]
    public static extern int HidP_GetCaps(nint preparsedData, out HIDP_CAPS capabilities);

    [DllImport("hid.dll")]
    public static extern int HidP_GetValueCaps(int reportType, nint valueCaps, ref ushort valueCapsLength, nint preparsedData);

    [DllImport("hid.dll")]
    public static extern int HidP_GetUsageValue(int reportType, ushort usagePage, ushort linkCollection, ushort usage,
        out uint usageValue, nint preparsedData, byte[] report, uint reportLength);

    public const uint GENERIC_READ = 0x80000000;
    public const uint GENERIC_WRITE = 0x40000000;
    public const uint FILE_SHARE_READ = 0x1;
    public const uint FILE_SHARE_WRITE = 0x2;
    public const uint OPEN_EXISTING = 3;
}
