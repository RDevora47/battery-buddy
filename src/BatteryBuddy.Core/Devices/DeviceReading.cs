namespace BatteryBuddy.Core.Devices;

public sealed record DeviceReading(
    string Key,
    string Name,
    DeviceKind Kind,
    bool IsConnected,
    int? BatteryPercent,
    BudsDetail? Detail,
    DateTimeOffset ReadAt,
    string Source)
{
    public int? EffectiveBattery => Detail?.Lowest ?? BatteryPercent;
}
