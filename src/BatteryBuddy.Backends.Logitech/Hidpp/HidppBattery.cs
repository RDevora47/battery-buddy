using BatteryBuddy.Devices;

namespace BatteryBuddy.Backends.Logitech.Hidpp;

public sealed record BatteryState(int Percent, bool Charging);

/// <summary>Decodes the two HID++ battery features. Both put the same layout in responses and events.</summary>
public static class HidppBattery
{
    /// <summary>
    /// UNIFIED_BATTERY (0x1004) get_status / event: [state of charge %, level flags, charging status, external power].
    /// Charging status: 0 discharging, 1 charging, 2 slow charging, 3 complete, 4 error.
    /// Devices without a percentage report 0 and only set a level flag (bit 0 critical … bit 3 full).
    /// </summary>
    public static BatteryState? ParseUnified(ReadOnlySpan<byte> p)
    {
        if (p.Length < 3) return null;
        int percent = p[0] is > 0 and <= 100 ? p[0] : FromLevelFlags(p[1]) ?? -1;
        if (percent < 0) return null;
        return new BatteryState(percent, p[2] is 1 or 2);
    }

    /// <summary>
    /// BATTERY_STATUS (0x1000) GetBatteryLevelStatus / event: [level %, next level %, status].
    /// Status: 0 discharging, 1 recharging, 2 almost full, 3 charged, 4 slow recharge, 5 invalid battery, 6 thermal error.
    /// </summary>
    public static BatteryState? ParseStatus(ReadOnlySpan<byte> p)
    {
        if (p.Length < 3 || p[0] > 100 || p[2] > 6) return null;
        int percent = p[2] == 3 && p[0] == 0 ? 100 : p[0];
        return new BatteryState(percent, p[2] is 1 or 2 or 4);
    }

    static int? FromLevelFlags(byte flags) =>
        (flags & 0x08) != 0 ? 90 : (flags & 0x04) != 0 ? 50 : (flags & 0x02) != 0 ? 20 : (flags & 0x01) != 0 ? 5 : null;

    /// <summary>DEVICE_NAME (0x0005) getDeviceType → our kinds; null for types we don't draw specially.</summary>
    public static DeviceKind? KindFromType(byte type) => type switch
    {
        0 or 2 => DeviceKind.Keyboard,       // keyboard, numpad
        3 or 4 or 5 => DeviceKind.Mouse,     // mouse, trackpad, trackball
        8 => DeviceKind.Earbuds,             // headset
        11 or 12 => DeviceKind.Gamepad,      // joystick, gamepad
        _ => null,
    };
}
