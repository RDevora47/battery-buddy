using BatteryBuddy.Backend;
using BatteryBuddy.Devices;

namespace BatteryBuddy.Tests;

public class DeviceHubTests
{
    sealed class FakeSource : IDeviceSource, IConnectionObserver
    {
        public FakeSource(string name) => Name = name;
        public string Name { get; }
        public int Refreshes;
        public readonly List<string> HeardConnected = new();
        public event EventHandler<IReadOnlyList<DeviceReading>>? SnapshotChanged;
        public void Publish(params DeviceReading[] readings) => SnapshotChanged?.Invoke(this, readings);
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task RefreshAsync(CancellationToken ct) { Refreshes++; return Task.CompletedTask; }
        public void OnDeviceConnected(DeviceReading device) => HeardConnected.Add(device.Name);
    }

    sealed class FakeRadio : IAvailabilityMonitor
    {
        public event EventHandler<bool>? AvailabilityChanged;
        public Task StartAsync() => Task.CompletedTask;
        public void Set(bool on) => AvailabilityChanged?.Invoke(this, on);
    }

    readonly FakeSource _windows = new("windows"), _buds = new("galaxy-buds");
    readonly FakeRadio _radio = new();
    readonly DeviceHub _hub;
    readonly List<DeviceChange> _changes = new();

    public DeviceHubTests()
    {
        // No synchronization context in tests: the hub serializes source events with a lock instead.
        _hub = new DeviceHub(new[] { _windows, _buds }, new ChargeTracker(), _ => { }, _radio, context: null);
        _hub.Changed += (_, c) => _changes.Add(c);
    }

    [Fact]
    public void Source_snapshots_reach_the_frontend_as_one_merged_list()
    {
        _windows.Publish(TestReadings.Make("Mouse", DeviceKind.Mouse));
        _buds.Publish(TestReadings.Make("Buds", DeviceKind.Earbuds, source: "galaxy-buds"));
        Assert.Equal(new[] { "buds", "mouse" }, _hub.Connected.Select(d => d.Key));
        Assert.Equal(2, _changes.Count);
    }

    [Fact]
    public void Other_sources_hear_about_new_devices_but_not_their_own()
    {
        _windows.Publish(TestReadings.Make("Buds", DeviceKind.Earbuds));
        Assert.Equal(new[] { "Buds" }, _buds.HeardConnected);
        Assert.Empty(_windows.HeardConnected);
    }

    [Fact]
    public async Task Refresh_reaches_every_source()
    {
        await _hub.RefreshAsync(CancellationToken.None);
        Assert.Equal((1, 1), (_windows.Refreshes, _buds.Refreshes));
    }

    [Fact]
    public void Availability_changes_are_forwarded_once()
    {
        var seen = new List<bool>();
        _hub.AvailabilityChanged += (_, on) => seen.Add(on);
        _radio.Set(true);    // already available: no event
        _radio.Set(false);
        _radio.Set(false);
        Assert.Equal(new[] { false }, seen);
        Assert.False(_hub.IsAvailable);
    }

    sealed class FakeSwitcher : IDeviceSource, IHostSwitcher
    {
        public string Name => "logitech";
        public string? Switchable;
        public int? SwitchedTo;
        public event EventHandler<IReadOnlyList<DeviceReading>>? SnapshotChanged { add { } remove { } }
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task RefreshAsync(CancellationToken ct) => Task.CompletedTask;
        public bool CanSwitchHost(DeviceReading device) => device.Key == Switchable;
        public bool IsVerifiedSwitcher(DeviceReading device) => true;
        public Task<HostChannels?> GetHostsAsync(DeviceReading device, CancellationToken ct) =>
            Task.FromResult<HostChannels?>(new HostChannels(new[] { new HostChannel(1, true), new HostChannel(2, true) }, 1));
        public Task<SwitchResult> SwitchHostAsync(DeviceReading device, int channel, CancellationToken ct)
        {
            SwitchedTo = channel;
            return Task.FromResult(SwitchResult.Switched);
        }
    }

    [Fact]
    public async Task Host_switching_goes_to_the_source_that_can_switch_the_device()
    {
        var switcher = new FakeSwitcher { Switchable = "mouse" };
        using var hub = new DeviceHub(new IDeviceSource[] { _windows, switcher }, new ChargeTracker(), _ => { });
        var mouse = TestReadings.Make("Mouse", DeviceKind.Mouse);

        Assert.True(hub.CanSwitchHost(mouse));
        Assert.True(hub.IsVerifiedSwitcher(mouse));
        Assert.Equal(1, (await hub.GetHostsAsync(mouse, CancellationToken.None))!.Current);
        Assert.Equal(SwitchResult.Switched, await hub.SwitchHostAsync(mouse, 2, CancellationToken.None));
        Assert.Equal(2, switcher.SwitchedTo);
    }

    [Fact]
    public async Task A_device_no_source_can_switch_is_not_switchable()
    {
        var switcher = new FakeSwitcher { Switchable = "other" };
        using var hub = new DeviceHub(new IDeviceSource[] { _windows, switcher }, new ChargeTracker(), _ => { });
        var mouse = TestReadings.Make("Mouse", DeviceKind.Mouse);

        Assert.False(hub.CanSwitchHost(mouse));
        Assert.False(hub.IsVerifiedSwitcher(mouse));
        Assert.Null(await hub.GetHostsAsync(mouse, CancellationToken.None));
        Assert.Equal(SwitchResult.Failed, await hub.SwitchHostAsync(mouse, 2, CancellationToken.None));
        Assert.Null(switcher.SwitchedTo);
    }
}
