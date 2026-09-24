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
}
