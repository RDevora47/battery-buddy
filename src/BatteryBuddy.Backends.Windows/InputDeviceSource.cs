using System.Collections.Concurrent;
using BatteryBuddy.Backends.Windows.Input;
using BatteryBuddy.Backends.Windows.Pnp;
using BatteryBuddy.Devices;
using Windows.Devices.Enumeration;

namespace BatteryBuddy.Backends.Windows;

/// <summary>
/// Every keyboard and mouse Windows has, battery or not (see <see cref="InputDeviceMerger"/>). Keyboard and mouse
/// device interfaces are hidden from WinRT, so this watches the device nodes of their setup classes instead.
/// </summary>
public sealed class InputDeviceSource : IDeviceSource, IDisposable
{
    const string KeyboardClass = "{4D36E96B-E325-11CE-BFC1-08002BE10318}";
    const string MouseClass = "{4D36E96F-E325-11CE-BFC1-08002BE10318}";
    const string ClassGuidKey = "System.Devices.ClassGuid";
    const string ContainerIdKey = "System.Devices.ContainerId";

    static readonly string Selector =
        $"System.Devices.ClassGuid:=\"{KeyboardClass}\" OR System.Devices.ClassGuid:=\"{MouseClass}\"";

    static readonly string[] Properties =
        { WindowsBatterySource.InstanceIdKey, ClassGuidKey, ContainerIdKey, WindowsBatterySource.PresentKey };

    static readonly TimeSpan WatcherRestartDelay = TimeSpan.FromSeconds(30);

    readonly VersionedStore<DeviceInformation> _devices = new();
    readonly ConcurrentDictionary<Guid, string> _containerNames = new();
    readonly SemaphoreSlim _publishGate = new(1, 1);
    readonly Action<string> _log;
    DeviceWatcher? _watcher;
    volatile bool _enumerated;
    volatile bool _disposed;

    public InputDeviceSource(Action<string> log) => _log = log;

    public string Name => InputDeviceMerger.SourceName;

    public event EventHandler<IReadOnlyList<DeviceReading>>? SnapshotChanged;

    public async Task StartAsync(CancellationToken ct)
    {
        StartWatcher();
        try { await RefreshAsync(ct); }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _log($"input: initial scan failed, relying on watcher: {ex.Message}");
        }
    }

    void StartWatcher()
    {
        _enumerated = false;
        var watcher = DeviceInformation.CreateWatcher(Selector, Properties, DeviceInformationKind.Device);
        watcher.Added += (_, info) =>
        {
            _devices.Upsert(info.Id, info);
            if (_enumerated) Publish();
        };
        watcher.Updated += (_, update) =>
        {
            if (_devices.TryUpdate(update.Id, info => info.Update(update))) Publish();
        };
        watcher.Removed += (_, update) =>
        {
            if (_devices.Remove(update.Id)) Publish();
        };
        watcher.EnumerationCompleted += (_, _) =>
        {
            _enumerated = true;
            Publish();
        };
        watcher.Stopped += (sender, _) =>
        {
            _log($"input: watcher stopped ({sender.Status})");
            if (sender.Status == DeviceWatcherStatus.Aborted && !_disposed) _ = RestartWatcherAsync();
        };
        _watcher = watcher;
        watcher.Start();
    }

    async Task RestartWatcherAsync()
    {
        await Task.Delay(WatcherRestartDelay);
        if (_disposed) return;
        try { StartWatcher(); }
        catch (Exception ex) { _log($"input: watcher restart failed: {ex.Message}"); }
    }

    public async Task RefreshAsync(CancellationToken ct)
    {
        long version = _devices.Version;
        var found = await DeviceInformation.FindAllAsync(Selector, Properties, DeviceInformationKind.Device).AsTask(ct);
        _devices.ReplaceAll(version, found.Select(info => (info.Id, info)));
        await PublishAsync();
    }

    void Publish() => _ = PublishAsync();

    async Task PublishAsync()
    {
        await _publishGate.WaitAsync();
        try
        {
            if (_disposed) return;
            var raw = _devices.Select((_, info) => Read(info));
            foreach (var container in raw.Select(r => r.Container).OfType<Guid>().Distinct())
                if (container != InputDeviceMerger.LocalMachineContainer && !_containerNames.ContainsKey(container))
                    _containerNames[container] = await ContainerNameAsync(container);
            var nodes = raw.Select(r => new InputNode(r.InstanceId, r.Class, r.Container,
                r.Container is Guid id ? _containerNames.GetValueOrDefault(id) : null, r.Present));
            SnapshotChanged?.Invoke(this, InputDeviceMerger.Merge(nodes, DateTimeOffset.Now));
        }
        catch (Exception ex) { _log($"input: publish failed: {ex.Message}"); }
        finally { _publishGate.Release(); }
    }

    // An empty name falls back to the merger's "Keyboard" / "Mouse"; it's cached so a failing lookup isn't retried.
    async Task<string> ContainerNameAsync(Guid container)
    {
        try
        {
            var info = await DeviceInformation.CreateFromIdAsync($"{{{container}}}", Array.Empty<string>(), DeviceInformationKind.DeviceContainer);
            return info.Name;
        }
        catch (Exception ex)
        {
            _log($"input: no name for container {container}: {ex.Message}");
            return "";
        }
    }

    static (string InstanceId, DeviceKind Class, Guid? Container, bool Present) Read(DeviceInformation info)
    {
        var p = info.Properties;
        string instanceId = p.TryGetValue(WindowsBatterySource.InstanceIdKey, out var id) && id is string s ? s : info.Id;
        var kind = p.TryGetValue(ClassGuidKey, out var c) && c is Guid g && g == Guid.Parse(KeyboardClass)
            ? DeviceKind.Keyboard : DeviceKind.Mouse;
        Guid? container = p.TryGetValue(ContainerIdKey, out var ci) && ci is Guid cg ? cg : null;
        bool present = p.TryGetValue(WindowsBatterySource.PresentKey, out var pr) && pr is true;
        return (instanceId, kind, container, present);
    }

    public void Dispose()
    {
        _disposed = true;
        if (_watcher is { Status: DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted })
            _watcher.Stop();
    }
}
