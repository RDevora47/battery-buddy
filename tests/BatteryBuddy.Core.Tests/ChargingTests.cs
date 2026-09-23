using BatteryBuddy.Core.Devices;
using BatteryBuddy.Core.Tracking;

namespace BatteryBuddy.Core.Tests;

public class ChargeTrackerTests
{
    const string Key = "mx master 3s";
    const string Windows = "windows";

    static DateTimeOffset At(double minutes) => TestReadings.T0.AddMinutes(minutes);

    [Theory]
    [InlineData(100, 2.0)]
    [InlineData(95, 2.0)]
    [InlineData(90, 2.0)]
    [InlineData(89, 1.9)]
    [InlineData(55, 1.6)]
    [InlineData(5, 1.1)]
    public void Tolerance_grows_with_charge(int level, double expected) =>
        Assert.Equal(expected, ChargeTracker.Tolerance(level), 3);

    [Fact]
    public void First_rise_without_history_waits_five_minutes_times_tolerance()
    {
        var tracker = new ChargeTracker();
        tracker.Observe(Key, Windows, 50, At(0));
        tracker.Observe(Key, Windows, 55, At(3));
        // 5 min × 1.6 (55 %) = 8 min after the rise.
        Assert.Equal(At(11), tracker.ChargingUntil(Key));
        Assert.True(tracker.IsCharging(Key, At(10.9)));
        Assert.False(tracker.IsCharging(Key, At(11.1)));
    }

    [Fact]
    public void First_rise_uses_the_learned_report_interval()
    {
        var tracker = new ChargeTracker(new Dictionary<string, DeviceChargeStats>
        {
            [Key] = new(MinutesPerPercent: null, LastStep: null, ReportIntervalMinutes: 2),
        });
        tracker.Observe(Key, Windows, 50, At(0));
        tracker.Observe(Key, Windows, 55, At(3));
        Assert.Equal(At(3 + 2 * 1.6), tracker.ChargingUntil(Key));
    }

    [Fact]
    public void Consecutive_rises_learn_minutes_per_percent_and_step()
    {
        var tracker = new ChargeTracker();
        tracker.Observe(Key, Windows, 50, At(0));
        tracker.Observe(Key, Windows, 55, At(10));
        tracker.Observe(Key, Windows, 60, At(20));   // 10 min for 5 % → 2 min per 1 %
        var stats = tracker.Stats[Key];
        Assert.Equal(2, stats.MinutesPerPercent!.Value, 3);
        Assert.Equal(5, stats.LastStep);
        // 2 min/% × 5 % × 1.7 (60 %) = 17 min.
        Assert.Equal(At(37), tracker.ChargingUntil(Key));
    }

    [Fact]
    public void Minutes_per_percent_is_an_exponential_average()
    {
        var tracker = new ChargeTracker();
        tracker.Observe(Key, Windows, 50, At(0));
        tracker.Observe(Key, Windows, 55, At(10));
        tracker.Observe(Key, Windows, 60, At(20));   // sample 2
        tracker.Observe(Key, Windows, 65, At(40));   // sample 4 → 0.3·4 + 0.7·2 = 2.6
        Assert.Equal(2.6, tracker.Stats[Key].MinutesPerPercent!.Value, 3);
    }

    [Fact]
    public void Report_interval_is_learned_from_any_windows_change()
    {
        var tracker = new ChargeTracker();
        tracker.Observe(Key, Windows, 60, At(0));
        tracker.Observe(Key, Windows, 59, At(4));
        tracker.Observe(Key, Windows, 58, At(8));
        Assert.Equal(4, tracker.Stats[Key].ReportIntervalMinutes!.Value, 3);
    }

    [Fact]
    public void Unchanged_reports_do_not_count_as_report_intervals()
    {
        var tracker = new ChargeTracker();
        tracker.Observe(Key, Windows, 60, At(0));
        tracker.Observe(Key, Windows, 60, At(5));   // poll, same value
        tracker.Observe(Key, Windows, 59, At(12));
        Assert.Equal(12, tracker.Stats[Key].ReportIntervalMinutes!.Value, 3);
    }

    [Fact]
    public void A_drop_stops_charging()
    {
        var tracker = new ChargeTracker();
        tracker.Observe(Key, Windows, 50, At(0));
        tracker.Observe(Key, Windows, 55, At(3));
        tracker.Observe(Key, Windows, 54, At(4));
        Assert.False(tracker.IsCharging(Key, At(4)));
        Assert.Null(tracker.ChargingUntil(Key));
    }

    [Fact]
    public void Switching_sources_is_not_a_rise()
    {
        var tracker = new ChargeTracker();
        tracker.Observe(Key, Windows, 80, At(0));
        tracker.Observe(Key, "galaxy-buds", 82, At(1));
        Assert.False(tracker.IsCharging(Key, At(1)));
    }

    [Fact]
    public void Forget_clears_the_session_but_keeps_learned_stats()
    {
        var tracker = new ChargeTracker();
        tracker.Observe(Key, Windows, 50, At(0));
        tracker.Observe(Key, Windows, 55, At(10));
        tracker.Observe(Key, Windows, 60, At(20));
        tracker.Forget(Key);
        Assert.False(tracker.IsCharging(Key, At(21)));
        Assert.NotNull(tracker.Stats[Key].MinutesPerPercent);
        tracker.Observe(Key, Windows, 70, At(60));   // no previous level after Forget → not a rise
        Assert.False(tracker.IsCharging(Key, At(60)));
    }
}

public class RegistryChargingTests
{
    readonly DeviceRegistry _registry = new();
    readonly List<RegistryChange> _changes = new();

    public RegistryChargingTests() => _registry.Changed += (_, c) => _changes.Add(c);

    static DeviceReading Mouse(int battery, double minutes) =>
        TestReadings.Make("MX Master 3S", DeviceKind.Mouse, battery: battery, at: TestReadings.T0.AddMinutes(minutes));

    [Fact]
    public void Rising_battery_marks_the_device_charging()
    {
        _registry.Apply("windows", new[] { Mouse(50, 0) });
        _registry.Apply("windows", new[] { Mouse(55, 3) });
        Assert.True(Assert.Single(_changes[1].Updated).IsCharging);
        Assert.Equal(TestReadings.T0.AddMinutes(11), _registry.NextChargeExpiry);
    }

    [Fact]
    public void Refresh_after_expiry_clears_charging_and_reports_it()
    {
        _registry.Apply("windows", new[] { Mouse(50, 0) });
        _registry.Apply("windows", new[] { Mouse(55, 3) });
        _registry.Refresh(TestReadings.T0.AddMinutes(12));
        Assert.False(Assert.Single(_changes[2].Updated).IsCharging);
        Assert.Null(_registry.NextChargeExpiry);
    }

    [Fact]
    public void Buds_in_case_are_charging()
    {
        var buds = TestReadings.Make("Buds", DeviceKind.Earbuds,
            detail: new BudsDetail(40, 60, 90, false, false, LeftInCase: true), source: "galaxy-buds");
        _registry.Apply("galaxy-buds", new[] { buds });
        Assert.True(Assert.Single(_registry.Connected).IsCharging);
    }
}
