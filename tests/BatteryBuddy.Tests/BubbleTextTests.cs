using BatteryBuddy.Devices;
using BatteryBuddy.Pet.Ui;

namespace BatteryBuddy.Tests;

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
    public void Missing_detail_says_so()
    {
        var buds = TestReadings.Make("Buds3 Pro de Roberto", DeviceKind.Earbuds, battery: 100) with { DetailUnavailable = true };
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
    public void A_device_without_a_battery_says_so() =>
        Assert.Equal(
            "Gaming KB\nno battery level",
            BubbleText.For(TestReadings.Make("Gaming KB", DeviceKind.Keyboard, battery: null) with { NoBattery = true }, Now).ReplaceLineEndings("\n"));

    [Fact]
    public void Charging_is_mentioned()
    {
        var mouse = TestReadings.Make("MX Master 3S", DeviceKind.Mouse, battery: 55) with { IsCharging = true };
        Assert.Contains("Battery 55% · charging", BubbleText.For(mouse, Now));
    }

    [Fact]
    public void Stale_battery_says_it_is_the_last_bluetooth_reading()
    {
        var keyboard = TestReadings.Make("RK-S98RGB", DeviceKind.Keyboard, battery: 91) with { BatteryStale = true };
        Assert.Equal(
            "RK-S98RGB\nBattery 91% · last known\nlast Bluetooth reading",
            BubbleText.For(keyboard, Now).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Unknown_battery_shows_question_mark() =>
        Assert.Contains("Battery ?", BubbleText.For(TestReadings.Make("Thing", battery: null), Now));

    [Fact]
    public void Time_left_follows_the_level()
    {
        var mouse = TestReadings.Make("MX Master 3S", DeviceKind.Mouse, battery: 45);
        Assert.Equal(
            "MX Master 3S\nBattery 45% · 2d 5h left\nupdated 10s ago",
            BubbleText.For(mouse, Now, new TimeSpan(2, 5, 0, 0)).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Buds_show_time_left_after_the_case()
    {
        var buds = TestReadings.Make("Buds3 Pro de Roberto", DeviceKind.Earbuds, detail: new BudsDetail(60, 58, 90, true, true));
        Assert.Contains("L 60% · R 58% · Case 90% · 3h 10min left", BubbleText.For(buds, Now, TimeSpan.FromMinutes(190)));
    }

    [Fact]
    public void Charging_wins_over_time_left()
    {
        var mouse = TestReadings.Make("MX Master 3S", DeviceKind.Mouse, battery: 55) with { IsCharging = true };
        var text = BubbleText.For(mouse, Now, TimeSpan.FromHours(3));
        Assert.Contains("Battery 55% · charging", text);
        Assert.DoesNotContain("left", text);
    }

    [Theory]
    [InlineData(0, "<1min")]
    [InlineData(0.4, "<1min")]
    [InlineData(45, "45min")]
    [InlineData(59.6, "1h")]
    [InlineData(60, "1h")]
    [InlineData(190, "3h 10min")]
    [InlineData(1439.6, "1d")]
    [InlineData(2 * 1440 + 5 * 60 + 20, "2d 5h")]
    [InlineData(2 * 1440 + 5 * 60 + 40, "2d 6h")]
    [InlineData(30 * 1440, "30d")]
    public void Time_left_formats(double minutes, string expected) =>
        Assert.Equal(expected, BubbleText.TimeLeft(TimeSpan.FromMinutes(minutes)));

    [Theory]
    [InlineData(10, "10s ago")]
    [InlineData(300, "5m ago")]
    [InlineData(7200, "2h ago")]
    [InlineData(-3, "0s ago")]
    public void Ago_formats(int seconds, string expected) =>
        Assert.Equal(expected, BubbleText.Ago(TimeSpan.FromSeconds(seconds)));
}

public class OneBudBubbleTests
{
    [Fact]
    public void A_bud_in_use_shows_time_left_while_its_partner_charges()
    {
        var buds = TestReadings.Make("Buds3 Pro de Roberto", DeviceKind.Earbuds,
            detail: new BudsDetail(30, 80, 90, false, true, LeftInCase: true)) with { IsCharging = true };
        var text = BubbleText.For(buds, TestReadings.T0, TimeSpan.FromMinutes(190));
        Assert.Contains("L 30% · R 80% · Case 90% · 3h 10min left", text);
    }

    [Fact]
    public void Buds_both_in_the_case_say_charging()
    {
        var buds = TestReadings.Make("Buds3 Pro de Roberto", DeviceKind.Earbuds,
            detail: new BudsDetail(30, 80, 90, false, false, true, true)) with { IsCharging = true };
        Assert.Contains("Case 90% · charging", BubbleText.For(buds, TestReadings.T0));
    }
}
