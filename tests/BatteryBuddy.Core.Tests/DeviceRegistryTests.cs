using BatteryBuddy.Core.Devices;
using BatteryBuddy.Core.Tracking;

namespace BatteryBuddy.Core.Tests;

public class DeviceRegistryTests
{
    readonly DeviceRegistry _registry = new();
    readonly List<RegistryChange> _changes = new();

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
}
