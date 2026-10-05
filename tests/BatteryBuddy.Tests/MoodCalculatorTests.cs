using BatteryBuddy.Pet.Animation;
using BatteryBuddy.Devices;

namespace BatteryBuddy.Tests;

public class MoodCalculatorTests
{
    static Mood MoodOf(params DeviceReading[] devices) => MoodCalculator.From(devices);

    [Fact]
    public void No_devices_is_happy() => Assert.Equal(Mood.Happy, MoodOf());

    [Theory]
    [InlineData(21, Mood.Happy)]
    [InlineData(20, Mood.Sleepy)]
    [InlineData(11, Mood.Sleepy)]
    [InlineData(10, Mood.Worried)]
    public void Lowest_battery_sets_mood(int lowest, Mood expected) =>
        Assert.Equal(expected, MoodOf(TestReadings.Make("A", battery: 90), TestReadings.Make("B", battery: lowest)));

    [Fact]
    public void Disconnected_devices_are_ignored() =>
        Assert.Equal(Mood.Happy, MoodOf(TestReadings.Make("A", connected: false, battery: 5)));

    [Fact]
    public void Unknown_battery_is_ignored() =>
        Assert.Equal(Mood.Happy, MoodOf(TestReadings.Make("A", battery: null)));

    [Fact]
    public void Super_saiyan_needs_every_known_battery_at_95_or_more()
    {
        Assert.True(MoodCalculator.SuperSaiyan(new[] { TestReadings.Make("A", battery: 100), TestReadings.Make("B", battery: null) }));
        Assert.True(MoodCalculator.SuperSaiyan(new[] { TestReadings.Make("A", battery: 100), TestReadings.Make("B", battery: 95) }));
        Assert.False(MoodCalculator.SuperSaiyan(new[] { TestReadings.Make("A", battery: 100), TestReadings.Make("B", battery: 94) }));
        Assert.False(MoodCalculator.SuperSaiyan(Array.Empty<DeviceReading>()));
        Assert.False(MoodCalculator.SuperSaiyan(new[] { TestReadings.Make("A", battery: null) }));
    }

    [Fact]
    public void Buds_use_lowest_bud() =>
        Assert.Equal(Mood.Worried, MoodOf(TestReadings.Make("Buds", battery: 90, detail: new BudsDetail(90, 8, 50, true, true))));

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    public void A_low_device_on_its_charger_keeps_the_pet_happy(int level)
    {
        var devices = new[] { TestReadings.Make("A", battery: 90), TestReadings.Make("B", battery: level) with { IsCharging = true } };
        Assert.Equal(Mood.Happy, MoodOf(devices));
        Assert.Equal(90, MoodCalculator.Lowest(devices));
        Assert.False(MoodCalculator.HasCritical(devices));
    }

    [Fact]
    public void A_charging_device_does_not_hide_another_low_one()
    {
        var devices = new[] { TestReadings.Make("A", battery: 5) with { IsCharging = true }, TestReadings.Make("B", battery: 15) };
        Assert.Equal(Mood.Sleepy, MoodOf(devices));
        Assert.False(MoodCalculator.HasCritical(devices));
    }

    [Fact]
    public void A_low_bud_in_its_case_is_charging_but_one_in_the_ear_still_counts()
    {
        var inCase = new BudsDetail(90, 8, 50, true, false, RightInCase: true);
        Assert.Equal(Mood.Happy, MoodOf(TestReadings.Make("Buds", battery: 90, detail: inCase) with { IsCharging = true }));

        var wornLow = new BudsDetail(8, 90, 50, true, false, RightInCase: true);
        Assert.Equal(Mood.Worried, MoodOf(TestReadings.Make("Buds", battery: 90, detail: wornLow) with { IsCharging = true }));
    }

    [Fact]
    public void Critical_is_a_device_at_10_or_less_not_charging()
    {
        Assert.True(MoodCalculator.HasCritical(new[] { TestReadings.Make("A", battery: 10) }));
        Assert.False(MoodCalculator.HasCritical(new[] { TestReadings.Make("A", battery: 11) }));
        Assert.False(MoodCalculator.HasCritical(new[] { TestReadings.Make("A", battery: null) }));
    }
}
