using BatteryBuddy.Devices;

namespace BatteryBuddy.Backend;

/// <summary>
/// Runs any set of <see cref="IDeviceSource"/>s behind one <see cref="IBatteryBackend"/>: merges their snapshots,
/// infers charging for sources that can't report it (re-checking when an inferred bolt runs out) and tells
/// <see cref="IConnectionObserver"/> sources about devices the others found. Everything happens on the given
/// synchronization context (pass the UI's); with none, calls are serialized with a lock on the caller's thread.
/// </summary>
public sealed class DeviceHub : IBatteryBackend
{
    readonly IReadOnlyList<IDeviceSource> _sources;
    readonly IAvailabilityMonitor? _availability;
    readonly DeviceRegistry _registry;
    readonly Action<string> _log;
    readonly SynchronizationContext? _context;
    readonly object _gate = new();
    readonly Timer _chargeTimer;
    bool _disposed;

    public DeviceHub(IEnumerable<IDeviceSource> sources, ChargeTracker charge, Action<string> log,
        IAvailabilityMonitor? availability = null, SynchronizationContext? context = null)
    {
        _sources = sources.ToList();
        _availability = availability;
        _registry = new DeviceRegistry(charge,
            device => _sources.OfType<IDetailSource>().Any(s => s.ProvidesDetailFor(device)));
        _log = log;
        _context = context;
        _chargeTimer = new Timer(_ => Post(RecheckCharging));

        _registry.Changed += OnRegistryChanged;
        foreach (var source in _sources)
            source.SnapshotChanged += (_, snapshot) => Post(() => _registry.Apply(source.Name, snapshot));
        if (_availability is not null)
            _availability.AvailabilityChanged += (_, on) => Post(() => SetAvailable(on));
    }

    public IReadOnlyList<DeviceReading> Connected => _registry.Connected;

    public DeviceReading? LastKnown(string key) => _registry.LastKnown(key);

    // Host switching goes to the first source that can switch this device; with none, it can't be switched.
    IHostSwitcher? SwitcherFor(DeviceReading device) =>
        _sources.OfType<IHostSwitcher>().FirstOrDefault(s => s.CanSwitchHost(device));

    public bool CanSwitchHost(DeviceReading device) => SwitcherFor(device) is not null;

    public bool IsVerifiedSwitcher(DeviceReading device) => SwitcherFor(device)?.IsVerifiedSwitcher(device) ?? false;

    public Task<HostChannels?> GetHostsAsync(DeviceReading device, CancellationToken ct) =>
        SwitcherFor(device)?.GetHostsAsync(device, ct) ?? Task.FromResult<HostChannels?>(null);

    public Task<SwitchResult> SwitchHostAsync(DeviceReading device, int channel, CancellationToken ct) =>
        SwitcherFor(device)?.SwitchHostAsync(device, channel, ct) ?? Task.FromResult(SwitchResult.Failed);

    public bool IsAvailable { get; private set; } = true;

    public event EventHandler<DeviceChange>? Changed;
    public event EventHandler<bool>? AvailabilityChanged;

    /// <summary>Starts the availability monitor, then every source. A source that fails to start is logged and skipped.</summary>
    public async Task StartAsync(CancellationToken ct)
    {
        if (_availability is not null) await StartSafely("availability", _availability.StartAsync);
        foreach (var source in _sources)
            await StartSafely(source.Name, () => source.StartAsync(ct));
    }

    public Task RefreshAsync(CancellationToken ct) =>
        Task.WhenAll(_sources.Select(async source =>
        {
            try { await source.RefreshAsync(ct); }
            catch (Exception ex) when (!ct.IsCancellationRequested) { _log($"{source.Name}: refresh failed: {ex.Message}"); }
        }));

    void OnRegistryChanged(object? sender, DeviceChange change)
    {
        foreach (var arrived in change.Added)
            foreach (var source in _sources)
                if (source is IConnectionObserver observer && source.Name != arrived.Source)
                    observer.OnDeviceConnected(arrived);
        ScheduleChargeCheck();
        Changed?.Invoke(this, change);
    }

    void RecheckCharging()
    {
        _registry.Refresh(DateTimeOffset.Now);
        ScheduleChargeCheck();
    }

    // One-shot: fires when the earliest inferred-charging flag is due to run out.
    void ScheduleChargeCheck()
    {
        if (_disposed) return;
        if (_registry.NextChargeExpiry is not DateTimeOffset expiry)
        {
            _chargeTimer.Change(Timeout.Infinite, Timeout.Infinite);
            return;
        }
        var wait = expiry - DateTimeOffset.Now;
        _chargeTimer.Change(wait > TimeSpan.FromSeconds(1) ? wait : TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
    }

    void SetAvailable(bool on)
    {
        if (on == IsAvailable) return;
        IsAvailable = on;
        AvailabilityChanged?.Invoke(this, on);
    }

    void Post(Action action)
    {
        if (_context is not null)
        {
            _context.Post(_ => { if (!_disposed) action(); }, null);
            return;
        }
        lock (_gate)
            if (!_disposed) action();
    }

    async Task StartSafely(string what, Func<Task> start)
    {
        try { await start(); }
        catch (Exception ex) { _log($"{what}: start failed: {ex}"); }
    }

    public void Dispose()
    {
        _disposed = true;
        _chargeTimer.Dispose();
        foreach (var disposable in _sources.OfType<IDisposable>()) disposable.Dispose();
        (_availability as IDisposable)?.Dispose();
    }
}
