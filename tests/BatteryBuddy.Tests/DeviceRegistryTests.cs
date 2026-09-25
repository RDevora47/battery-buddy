using BatteryBuddy.Devices;
using BatteryBuddy.Backend;

namespace BatteryBuddy.Tests;

public class DeviceRegistryTests
{
    readonly DeviceRegistry _registry = new();
    readonly List<DeviceChange> _changes = new();

    public DeviceRegistryTests() => _registry.Changed += (_, c) => _changes.Add(c);

    static DeviceReading Mouse(int battery = 55, bool connected = true, int minutes = 0) =>
        TestReadings.Make("MX Master 3S", DeviceKind.Mouse, connected, battery, at: TestReadings.T0.AddMinutes(minutes));

    [Fact]
    public void First_connected_snapshot_adds_device()
    {
        _registry.Apply("windows", new[] { Mouse() });
        var change = Assert.Single(_changes);
        Assert.Equal("MX Master 3S", Assert.Single(change.Added).Name);
        Assert.Single(_registry.Connected);
    }

    [Fact]
    public void Same_content_newer_time_raises_nothing_but_refreshes_reading()
    {
        _registry.Apply("windows", new[] { Mouse() });
        _registry.Apply("windows", new[] { Mouse(minutes: 5) });
        Assert.Single(_changes);
        Assert.Equal(TestReadings.T0.AddMinutes(5), _registry.LastKnown(Mouse().Key)!.ReadAt);
    }

    [Fact]
    public void Battery_change_is_update()
    {
        _registry.Apply("windows", new[] { Mouse(55) });
        _registry.Apply("windows", new[] { Mouse(50) });
        Assert.Equal(50, Assert.Single(_changes[1].Updated).BatteryPercent);
    }

    [Fact]
    public void Disconnect_is_removal_and_keeps_last_known()
    {
        _registry.Apply("windows", new[] { Mouse(55) });
        _registry.Apply("windows", new[] { Mouse(40, connected: false) });
        Assert.Single(_changes[1].Removed);
        Assert.Empty(_registry.Connected);
        var last = _registry.LastKnown(Mouse().Key)!;
        Assert.False(last.IsConnected);
        Assert.Equal(55, last.BatteryPercent);
    }

    [Fact]
    public void A_reading_with_a_battery_beats_a_newer_one_without()
    {
        _registry.Apply("windows", new[] { Mouse(55) });
        _registry.Apply("input", new[] { Mouse(minutes: 5) with { BatteryPercent = null, NoBattery = true, Source = "input" } });
        var mouse = Assert.Single(_registry.Connected);
        Assert.Equal((55, false), (mouse.BatteryPercent, mouse.NoBattery));
    }

    [Fact]
    public void Without_a_battery_reading_the_battery_less_one_shows()
    {
        _registry.Apply("input", new[] { Mouse() with { BatteryPercent = null, NoBattery = true, Source = "input" } });
        Assert.True(Assert.Single(_registry.Connected).NoBattery);
    }

    [Fact]
    public void Vanishing_from_snapshot_is_removal()
    {
        _registry.Apply("windows", new[] { Mouse() });
        _registry.Apply("windows", Array.Empty<DeviceReading>());
        Assert.Single(_changes[1].Removed);
    }

    [Fact]
    public void Detailed_reading_wins_over_windows_reading()
    {
        var windows = TestReadings.Make("Buds3 Pro", DeviceKind.Earbuds, battery: 100);
        var buds = windows with { Detail = new BudsDetail(90, 80, 70, true, true), Source = "galaxy-buds" };
        _registry.Apply("windows", new[] { windows });
        _registry.Apply("galaxy-buds", new[] { buds });
        Assert.Equal(80, Assert.Single(_registry.Connected).EffectiveBattery);
        Assert.Single(_changes[1].Updated);
    }

    [Fact]
    public void Losing_buds_detail_is_update_not_removal()
    {
        var windows = TestReadings.Make("Buds3 Pro", DeviceKind.Earbuds, battery: 100);
        _registry.Apply("windows", new[] { windows });
        _registry.Apply("galaxy-buds", new[] { windows with { Detail = new BudsDetail(90, 80, 70, true, true) } });
        _registry.Apply("galaxy-buds", Array.Empty<DeviceReading>());
        Assert.Empty(_changes[2].Removed);
        Assert.Null(Assert.Single(_changes[2].Updated).Detail);
    }

    [Fact]
    public void Disconnected_device_seen_first_time_is_silent_but_remembered()
    {
        _registry.Apply("windows", new[] { Mouse(75, connected: false) });
        Assert.Empty(_changes);
        Assert.Equal(75, _registry.LastKnown(Mouse().Key)!.BatteryPercent);
    }

    [Fact]
    public void Connected_is_ordered_by_key()
    {
        _registry.Apply("windows", new[]
        {
            TestReadings.Make("Zeta"), TestReadings.Make("Alpha"), TestReadings.Make("Mid"),
        });
        Assert.Equal(new[] { "alpha", "mid", "zeta" }, _registry.Connected.Select(r => r.Key));
    }

    // --- merging across backends ---

    static DeviceReading Logi(int battery, bool charging, int minutes = 0) =>
        TestReadings.Make("MX Master 3S", DeviceKind.Mouse, battery: battery, at: TestReadings.T0.AddMinutes(minutes), source: "logitech")
            with { IsCharging = charging, ChargingKnown = true, Address = "DF43C7BC3A2B" };

    [Fact]
    public void Reported_charging_wins_over_inference_and_newer_readings()
    {
        _registry.Apply("windows", new[] { Mouse(55, minutes: 1) });
        _registry.Apply("logitech", new[] { Logi(56, charging: true) });
        var merged = Assert.Single(_registry.Connected);
        Assert.Equal(("logitech", 56, true), (merged.Source, merged.BatteryPercent, merged.IsCharging));
    }

    [Fact]
    public void Reported_not_charging_is_trusted_even_while_levels_rise()
    {
        _registry.Apply("logitech", new[] { Logi(50, charging: false) });
        _registry.Apply("logitech", new[] { Logi(55, charging: false, minutes: 3) });
        Assert.False(Assert.Single(_registry.Connected).IsCharging);
    }

    [Fact]
    public void Same_address_merges_devices_under_the_first_name_seen()
    {
        var renamed = TestReadings.Make("Rob's mouse", DeviceKind.Mouse, battery: 55) with { Address = "DF43C7BC3A2B" };
        _registry.Apply("windows", new[] { renamed });
        _registry.Apply("logitech", new[] { Logi(56, charging: true) });
        var merged = Assert.Single(_registry.Connected);
        Assert.Equal(("Rob's mouse", "logitech"), (merged.Name, merged.Source));
    }

    [Fact]
    public void Falls_back_to_the_other_source_and_inference_when_the_charge_aware_one_drops_out()
    {
        _registry.Apply("windows", new[] { Mouse(50) });
        _registry.Apply("logitech", new[] { Logi(50, charging: false) });
        _registry.Apply("logitech", Array.Empty<DeviceReading>());
        _registry.Apply("windows", new[] { Mouse(55, minutes: 3) });   // a rise: inferred charging
        var merged = Assert.Single(_registry.Connected);
        Assert.Equal(("windows", true), (merged.Source, merged.IsCharging));
        Assert.Empty(_changes.SelectMany(c => c.Removed));
    }

    [Fact]
    public void A_stale_battery_never_counts_as_a_rise()
    {
        _registry.Apply("windows", new[] { Mouse(55) });
        _registry.Apply("windows", new[] { Mouse(60, minutes: 3) with { BatteryStale = true } });
        Assert.False(Assert.Single(_registry.Connected).IsCharging);
    }

    [Fact]
    public void Detail_unavailable_when_a_detail_source_claims_the_device_but_has_none()
    {
        var registry = new DeviceRegistry(detailExpected: d => d.Kind == DeviceKind.Earbuds);
        registry.Apply("windows", new[] { TestReadings.Make("Buds", DeviceKind.Earbuds), Mouse() });
        Assert.True(registry.LastKnown(DeviceKey.ForName("Buds"))!.DetailUnavailable);
        Assert.False(registry.LastKnown(Mouse().Key)!.DetailUnavailable);
    }
}
