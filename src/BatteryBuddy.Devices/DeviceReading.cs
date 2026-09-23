namespace BatteryBuddy.Devices;

/// <summary>
/// The common format every backend reports. Key identifies the device across sources (see <see cref="DeviceKey"/>).
/// ChargingKnown: the source reported IsCharging itself; otherwise the hub infers it from rising levels.
/// Address: a hardware address (Bluetooth MAC, 12 upper-case hex digits) when the source knows it; readings
/// with the same address are merged even if their names differ.
/// DetailUnavailable: set by the hub when some source normally gives this device a <see cref="Detail"/> but
/// the reading it merged has none (e.g. the buds link is down and only Windows' single value is known).
/// </summary>
public sealed record DeviceReading(
    string Key,
    string Name,
    DeviceKind Kind,
    bool IsConnected,
    int? BatteryPercent,
    BudsDetail? Detail,
    DateTimeOffset ReadAt,
    string Source,
    bool IsCharging = false,
    bool ChargingKnown = false,
    string? Address = null,
    bool DetailUnavailable = false)
{
    public int? EffectiveBattery => Detail?.Lowest ?? BatteryPercent;
}
