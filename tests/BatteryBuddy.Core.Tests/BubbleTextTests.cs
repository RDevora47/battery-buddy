using BatteryBuddy.Core.Devices;
using BatteryBuddy.Core.Ui;

namespace BatteryBuddy.Core.Tests;

public class BubbleTextTests
{
    static readonly DateTimeOffset Now = TestReadings.T0.AddSeconds(10);

    [Fact]
    public void Buds_with_detail_show_each_part_and_wear()
    {
        var buds = TestReadings.Make("Buds3 Pro de Roberto", DeviceKind.Earbuds, detail: new BudsDetail(99, 98, null, true, false));
        Assert.Equal(
            "Buds3 Pro de Roberto\nL 99% · R 98% · Case ?\nwearing left\nupdated 10s ago",
            BubbleText.For(buds, Now).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Galaxy_buds_without_detail_say_so()
    {
        var buds = TestReadings.Make("Buds3 Pro de Roberto", DeviceKind.Earbuds, battery: 100);
        Assert.Equal(
            "Buds3 Pro de Roberto\nBattery 100%\nL/R detail unavailable\nupdated 10s ago",
            BubbleText.For(buds, Now).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Other_devices_show_battery() =>
        Assert.Equal(
            "MX Master 3S\nBattery 55%\nupdated 10s ago",
            BubbleText.For(TestReadings.Make("MX Master 3S", DeviceKind.Mouse, battery: 55), Now).ReplaceLineEndings("\n"));

    [Fact]
    public void Charging_is_mentioned()
    {
        var mouse = TestReadings.Make("MX Master 3S", DeviceKind.Mouse, battery: 55) with { IsCharging = true };
        Assert.Contains("Battery 55% · charging", BubbleText.For(mouse, Now));
    }

    [Fact]
    public void Unknown_battery_shows_question_mark() =>
        Assert.Contains("Battery ?", BubbleText.For(TestReadings.Make("Thing", battery: null), Now));

    [Theory]
    [InlineData(10, "10s ago")]
    [InlineData(300, "5m ago")]
    [InlineData(7200, "2h ago")]
    [InlineData(-3, "0s ago")]
    public void Ago_formats(int seconds, string expected) =>
        Assert.Equal(expected, BubbleText.Ago(TimeSpan.FromSeconds(seconds)));
}
