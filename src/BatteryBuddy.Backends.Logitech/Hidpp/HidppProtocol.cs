namespace BatteryBuddy.Backends.Logitech.Hidpp;

public enum HidppMatch { Unrelated, Response, Error }

/// <summary>
/// HID++ 2.0 framing. A report is [report id, device index, feature index, function &lt;&lt; 4 | software id, params...].
/// Long reports (0x11, 20 bytes) are the only kind BLE devices accept, and receivers take them too.
/// </summary>
public static class HidppProtocol
{
    public const byte ShortReport = 0x10;
    public const byte LongReport = 0x11;
    public const int LongLength = 20;

    /// <summary>Device index of a device talking to us directly (Bluetooth or USB), not through a receiver.</summary>
    public const byte DirectIndex = 0xFF;

    /// <summary>Ours. Options+ uses 0x0A, and 0 marks unsolicited events.</summary>
    public const byte SoftwareId = 0x0B;

    const byte Hidpp20Error = 0xFF;
    const byte Hidpp10Error = 0x8F;   // a receiver answering for a device that's asleep or gone

    /// <summary>Receiver notification: a paired device connected or disconnected.</summary>
    public const byte DeviceConnection = 0x41;

    public const ushort Root = 0x0000;
    public const ushort DeviceName = 0x0005;
    public const ushort BatteryStatus = 0x1000;
    public const ushort UnifiedBattery = 0x1004;
    public const ushort ChangeHost = 0x1814;
    public const ushort HostsInfo = 0x1815;

    public static byte[] Request(byte deviceIndex, byte featureIndex, byte function, params byte[] args)
    {
        var frame = new byte[LongLength];
        frame[0] = LongReport;
        frame[1] = deviceIndex;
        frame[2] = featureIndex;
        frame[3] = (byte)(function << 4 | SoftwareId);
        args.CopyTo(frame, 4);
        return frame;
    }

    /// <summary>Root function 0 (getFeature): where a feature sits in this device's table.</summary>
    public static byte[] FeatureLookup(byte deviceIndex, ushort feature) =>
        Request(deviceIndex, 0, 0, (byte)(feature >> 8), (byte)feature);

    /// <summary>Whether a report answers the request with these fields, or is its error reply.</summary>
    public static HidppMatch Match(ReadOnlySpan<byte> report, byte deviceIndex, byte featureIndex, byte function)
    {
        if (report.Length < 6 || report[0] is not (ShortReport or LongReport) || report[1] != deviceIndex)
            return HidppMatch.Unrelated;
        byte functionAndId = (byte)(function << 4 | SoftwareId);
        if (report[2] == featureIndex && report[3] == functionAndId) return HidppMatch.Response;
        if (report[2] is Hidpp20Error or Hidpp10Error && report[3] == featureIndex && report[4] == functionAndId)
            return HidppMatch.Error;
        return HidppMatch.Unrelated;
    }

    /// <summary>An unsolicited event (function 0, software id 0) from this feature, e.g. a battery status broadcast.</summary>
    public static bool IsEvent(ReadOnlySpan<byte> report, byte deviceIndex, byte featureIndex) =>
        report.Length >= 7 && report[0] is ShortReport or LongReport && report[1] == deviceIndex
        && report[2] == featureIndex && report[3] == 0;

    public static bool IsConnectionNotice(ReadOnlySpan<byte> report) =>
        report.Length >= 3 && report[0] is ShortReport or LongReport && report[2] == DeviceConnection;

    /// <summary>The parameter bytes of a response or event.</summary>
    public static ReadOnlySpan<byte> Params(ReadOnlySpan<byte> report) => report.Length > 4 ? report[4..] : default;
}
