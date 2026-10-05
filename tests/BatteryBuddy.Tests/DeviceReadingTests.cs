using BatteryBuddy.Devices;

namespace BatteryBuddy.Tests;

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

public class UnchargedBatteryTests
{
    [Fact]
    public void A_charging_device_has_none() =>
        Assert.Null((TestReadings.Make("Mouse", battery: 40) with { IsCharging = true }).UnchargedBattery);

    [Fact]
    public void A_bud_out_of_the_case_counts_while_its_partner_charges() =>
        Assert.Equal(80, (TestReadings.Make("Buds", detail: new BudsDetail(30, 80, 90, false, true, LeftInCase: true)) with { IsCharging = true })
            .UnchargedBattery);

    [Fact]
    public void Both_buds_in_the_case_have_none() =>
        Assert.Null(TestReadings.Make("Buds", detail: new BudsDetail(30, 80, 90, false, false, true, true)).UnchargedBattery);

    [Fact]
    public void A_draining_device_has_its_level() =>
        Assert.Equal(40, TestReadings.Make("Mouse", battery: 40).UnchargedBattery);
}
