using BatteryBuddy.Core.Devices;

namespace BatteryBuddy.Core.Tests;

internal static class TestReadings
{
    public static readonly DateTimeOffset T0 = new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

    public static DeviceReading Make(
        string name,
        DeviceKind kind = DeviceKind.Other,
        bool connected = true,
        int? battery = 50,
        BudsDetail? detail = null,
        DateTimeOffset? at = null,
        string source = "windows") =>
        new(DeviceKey.ForName(name), name, kind, connected, battery, detail, at ?? T0, source);
}
