using System.Runtime.InteropServices;

namespace BatteryBuddy.Backends.Windows.Audio;

// The slice of the Core Audio + kernel-streaming COM API needed to poke a Bluetooth audio driver.
// Methods are declared in vtable order, up to the last one called; unused ones take IntPtr placeholders.

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
class MMDeviceEnumerator { }

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out IMMDeviceCollection devices);
    [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, IntPtr endpoint);
    [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
}

[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceCollection
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int Item(uint index, out IMMDevice device);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore store);
}

[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPropertyStore
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int GetAt(uint index, out PropertyKey key);
    [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
}

[ComImport, Guid("2A07407E-6497-4A18-9787-32F79BD0D98F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IDeviceTopology
{
    [PreserveSig] int GetConnectorCount(out uint count);
    [PreserveSig] int GetConnector(uint index, out IConnector connector);
}

[ComImport, Guid("9C2C4058-23F5-41DE-877A-DF3AF236A09E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IConnector
{
    [PreserveSig] int GetType(out int type);
    [PreserveSig] int GetDataFlow(out int flow);
    [PreserveSig] int ConnectTo(IntPtr other);
    [PreserveSig] int Disconnect();
    [PreserveSig] int IsConnected(out int connected);
    [PreserveSig] int GetConnectedTo(IntPtr other);
    [PreserveSig] int GetConnectorIdConnectedTo(IntPtr id);
    [PreserveSig] int GetDeviceIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
}

[ComImport, Guid("28F54685-06FD-11D2-B27A-00A0C9223196"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IKsControl
{
    [PreserveSig] int KsProperty(ref KsProperty property, uint propertyLength, IntPtr data, uint dataLength, out uint bytesReturned);
}

[StructLayout(LayoutKind.Sequential)]
struct KsProperty
{
    public Guid Set;
    public uint Id;
    public uint Flags;
}

[StructLayout(LayoutKind.Sequential)]
struct PropertyKey
{
    public Guid FormatId;
    public uint PropertyId;

    public PropertyKey(Guid formatId, uint propertyId)
    {
        FormatId = formatId;
        PropertyId = propertyId;
    }
}

[StructLayout(LayoutKind.Sequential)]
struct PropVariant
{
    public ushort Type;
    ushort _reserved1, _reserved2, _reserved3;
    public IntPtr Pointer;
    IntPtr _second;
}

static class CoreAudio
{
    public const int DataFlowAll = 2;
    public const uint DeviceStateAll = 0xF;       // active, disabled, not present, unplugged
    public const uint ClsCtxAll = 0x17;
    public const uint StgmRead = 0;
    const ushort VtLpwstr = 31;

    public static readonly Guid IDeviceTopologyId = typeof(IDeviceTopology).GUID;
    public static readonly Guid IKsControlId = typeof(IKsControl).GUID;

    /// <summary>The endpoint's name with the device's in brackets: "Headphones (Buds3 Pro de Roberto)".</summary>
    public static readonly PropertyKey DeviceFriendlyName = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    /// <summary>The Bluetooth audio driver's property set; getting ONESHOT_RECONNECT pages the device.</summary>
    public static readonly Guid KsPropSetIdBtAudio = new("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");
    public const uint KsPropertyOneshotReconnect = 0;
    public const uint KsPropertyTypeGet = 1;

    public static string? ReadString(IPropertyStore store, PropertyKey key)
    {
        if (store.GetValue(ref key, out var value) < 0) return null;
        try { return value.Type == VtLpwstr ? Marshal.PtrToStringUni(value.Pointer) : null; }
        finally { PropVariantClear(ref value); }
    }

    [DllImport("ole32.dll")]
    static extern int PropVariantClear(ref PropVariant value);
}
