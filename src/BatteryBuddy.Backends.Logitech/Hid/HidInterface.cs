using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BatteryBuddy.Backends.Logitech.Hid;

/// <summary>One HID top-level collection (a Windows HID interface), as found by <see cref="Enumerate"/>.</summary>
public sealed record HidInterface(string Path, string Product, ushort VendorId, ushort ProductId,
    ushort UsagePage, ushort Usage, int InputLength, int OutputLength)
{
    const int DIGCF_PRESENT = 0x02, DIGCF_DEVICEINTERFACE = 0x10;
    const uint FILE_SHARE_READ_WRITE = 0x03, OPEN_EXISTING = 3;
    const int HIDP_STATUS_SUCCESS = 0x00110000;

    /// <summary>Vendor-defined collections (usage page ≥ 0xFF00) of one vendor that accept output reports.</summary>
    public static List<HidInterface> Enumerate(ushort vendorId)
    {
        var result = new List<HidInterface>();
        var guid = Guid.Empty;
        HidD_GetHidGuid(ref guid);
        IntPtr set = SetupDiGetClassDevsW(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (set == new IntPtr(-1)) return result;
        try
        {
            var data = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
            for (int i = 0; SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref data); i++)
            {
                // Documented fixed value, not SizeOf(): 8 on x64, 6 on x86.
                var detail = new SP_DEVICE_INTERFACE_DETAIL_DATA_W { cbSize = IntPtr.Size == 8 ? 8 : 6 };
                if (!SetupDiGetDeviceInterfaceDetailW(set, ref data, ref detail, Marshal.SizeOf(detail), IntPtr.Zero, IntPtr.Zero))
                    continue;
                if (Describe(detail.DevicePath, vendorId) is { } iface) result.Add(iface);
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return result;
    }

    // Opening with no access rights is enough to read attributes and caps, even for collections others hold.
    static HidInterface? Describe(string path, ushort vendorId)
    {
        using var handle = CreateFileW(path, 0, FILE_SHARE_READ_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (handle.IsInvalid) return null;
        var attrs = new HIDD_ATTRIBUTES { Size = Marshal.SizeOf<HIDD_ATTRIBUTES>() };
        if (!HidD_GetAttributes(handle, ref attrs) || attrs.VendorID != vendorId) return null;
        if (!HidD_GetPreparsedData(handle, out var preparsed)) return null;
        var caps = new HIDP_CAPS();
        int status;
        try { status = HidP_GetCaps(preparsed, ref caps); }
        finally { HidD_FreePreparsedData(preparsed); }
        if (status != HIDP_STATUS_SUCCESS || caps.UsagePage < 0xFF00 || caps.OutputReportByteLength == 0) return null;

        var name = new char[128];
        string product = HidD_GetProductString(handle, name, name.Length * 2) ? new string(name).TrimEnd('\0') : "";
        return new HidInterface(path, product, attrs.VendorID, attrs.ProductID, caps.UsagePage, caps.Usage,
            caps.InputReportByteLength, caps.OutputReportByteLength);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SP_DEVICE_INTERFACE_DETAIL_DATA_W
    {
        public int cbSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string DevicePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct HIDD_ATTRIBUTES { public int Size; public ushort VendorID, ProductID, VersionNumber; }

    [StructLayout(LayoutKind.Sequential)]
    struct HIDP_CAPS
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
            NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
            NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    [DllImport("hid.dll")] static extern void HidD_GetHidGuid(ref Guid guid);
    [DllImport("hid.dll")] static extern bool HidD_GetAttributes(SafeFileHandle h, ref HIDD_ATTRIBUTES a);
    [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr preparsed);
    [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr preparsed);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr preparsed, ref HIDP_CAPS caps);
    [DllImport("hid.dll", CharSet = CharSet.Unicode)] static extern bool HidD_GetProductString(SafeFileHandle h, char[] buffer, int bufferBytes);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SetupDiGetClassDevsW(ref Guid g, IntPtr enumerator, IntPtr parent, int flags);
    [DllImport("setupapi.dll")]
    static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid g, int index, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
    static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data,
        ref SP_DEVICE_INTERFACE_DETAIL_DATA_W detail, int size, IntPtr required, IntPtr info);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security,
        uint disposition, uint flags, IntPtr template);
}
