using BatteryBuddy.Core.Devices;

namespace BatteryBuddy.Core.Tests;

public class DeviceReadingTests
{
    [Fact]
    public void Lowest_is_min_of_left_and_right() =>
        Assert.Equal(93, new BudsDetail(94, 93, 92, true, true).Lowest);

    [Fact]
    public void Lowest_uses_single_known_bud() =>
        Assert.Equal(80, new BudsDetail(null, 80, null, false, false).Lowest);

    [Fact]
    public void Lowest_is_null_when_both_unknown() =>
        Assert.Null(new BudsDetail(null, null, 50, false, false).Lowest);

    [Fact]
    public void EffectiveBattery_prefers_buds_detail()
    {
        var reading = TestReadings.Make("Buds", battery: 100, detail: new BudsDetail(40, 60, 90, true, true));
        Assert.Equal(40, reading.EffectiveBattery);
    }

    [Fact]
    public void EffectiveBattery_falls_back_to_battery_percent() =>
        Assert.Equal(55, TestReadings.Make("MX Master 3S", battery: 55).EffectiveBattery);

    [Theory]
    [InlineData("Buds3 Pro de Roberto", "buds3 pro de roberto")]
    [InlineData("  MX  Master 3S ", "mx master 3s")]
    public void DeviceKey_normalizes_case_and_whitespace(string name, string expected) =>
        Assert.Equal(expected, DeviceKey.ForName(name));
}
