using System.Collections.Concurrent;
using BatteryBuddy.Core.Devices;
using BatteryBuddy.Core.Pnp;
using Windows.Devices.Enumeration;

namespace BatteryBuddy.Bluetooth;

public sealed class WindowsBatterySource : IDeviceSource, IDisposable
{
    public const string BatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    public const string ConnectedKey = "{83DA6326-97A6-4088-9453-A1923F573B29} 15";
    public const string ClassOfDeviceKey = "{2BD67D8B-8BEB-48D5-87E0-6CDA3428040A} 10";
    public const string InstanceIdKey = "System.Devices.DeviceInstanceId";

    // Only the node types that carry battery or connection state; never all BTH* nodes.
    public const string Selector =
        "System.Devices.DeviceInstanceId:~<\"BTHLE\\DEV_\" OR " +
        "System.Devices.DeviceInstanceId:~<\"BTHENUM\\DEV_\" OR " +
        "System.Devices.DeviceInstanceId:~<\"BTHENUM\\{0000111E\" OR " +
        "System.Devices.DeviceInstanceId:~<\"BTHENUM\\{0000111F\"";

    static readonly string[] Properties = { InstanceIdKey, BatteryKey, ConnectedKey, ClassOfDeviceKey };

    readonly ConcurrentDictionary<string, DeviceInformation> _devices = new();
    readonly object _publishGate = new();
    readonly TimeSpan _pollInterval;
    readonly Action<string> _log;
    readonly bool _verbose;
    DeviceWatcher? _watcher;
    Timer? _poll;
    volatile bool _enumerated;

    public WindowsBatterySource(TimeSpan pollInterval, Action<string> log, bool verbose = false)
    {
        _pollInterval = pollInterval;
        _log = log;
        _verbose = verbose;
    }

    public string Name => PnpNodeMerger.SourceName;

    public event EventHandler<IReadOnlyList<DeviceReading>>? SnapshotChanged;

    public async Task StartAsync(CancellationToken ct)
    {
        await RefreshAsync(ct);

        _watcher = DeviceInformation.CreateWatcher(Selector, Properties, DeviceInformationKind.Device);
        _watcher.Added += (_, info) =>
        {
            _devices[info.Id] = info;
            Trace("added", info);
            if (_enumerated) Publish();
        };
        _watcher.Updated += (_, update) =>
        {
            if (!_devices.TryGetValue(update.Id, out var info)) return;
            info.Update(update);
            Trace("updated", info);
            Publish();
        };
        _watcher.Removed += (_, update) =>
        {
            if (!_devices.TryRemove(update.Id, out var info)) return;
            Trace("removed", info);
            Publish();
        };
        _watcher.EnumerationCompleted += (_, _) =>
        {
            _enumerated = true;
            Publish();
        };
        _watcher.Stopped += (_, _) => _log("windows: watcher stopped");
        _watcher.Start();

        _poll = new Timer(_ => _ = PollAsync(), null, _pollInterval, _pollInterval);
    }

    public async Task RefreshAsync(CancellationToken ct)
    {
        var found = await DeviceInformation.FindAllAsync(Selector, Properties, DeviceInformationKind.Device).AsTask(ct);
        var ids = new HashSet<string>();
        foreach (var info in found)
        {
            _devices[info.Id] = info;
            ids.Add(info.Id);
        }
        foreach (var id in _devices.Keys)
            if (!ids.Contains(id)) _devices.TryRemove(id, out _);
        Publish();
    }

    async Task PollAsync()
    {
        try { await RefreshAsync(CancellationToken.None); }
        catch (Exception ex) { _log($"windows: poll failed: {ex.Message}"); }
    }

    void Publish()
    {
        lock (_publishGate)
        {
            var readings = PnpNodeMerger.Merge(_devices.Values.Select(ToNode), DateTimeOffset.Now);
            SnapshotChanged?.Invoke(this, readings);
        }
    }

    static PnpNode ToNode(DeviceInformation info)
    {
        var p = info.Properties;
        string instanceId = p.TryGetValue(InstanceIdKey, out var id) && id is string s ? s : info.Id;
        int? battery = p.TryGetValue(BatteryKey, out var b)
            ? b switch { byte x => (int?)x, int x => (int?)x, uint x => (int?)x, _ => null }
            : null;
        bool? connected = p.TryGetValue(ConnectedKey, out var c) && c is bool isConnected ? isConnected : null;
        uint? cod = p.TryGetValue(ClassOfDeviceKey, out var d) && d is uint u ? u : null;
        return new PnpNode(instanceId, info.Name, battery, connected, cod);
    }

    void Trace(string what, DeviceInformation info)
    {
        if (!_verbose) return;
        var n = ToNode(info);
        _log($"windows: {what} {n.Name} | {n.InstanceId} | battery={n.Battery} connected={n.IsConnected} cod={n.ClassOfDevice:X}");
    }

    public void Dispose()
    {
        _poll?.Dispose();
        if (_watcher is { Status: DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted })
            _watcher.Stop();
    }
}
