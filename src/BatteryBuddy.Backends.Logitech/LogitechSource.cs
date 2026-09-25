using System.Text;
using System.Text.RegularExpressions;
using BatteryBuddy.Backends.Logitech.Hid;
using BatteryBuddy.Backends.Logitech.Hidpp;
using BatteryBuddy.Devices;
using BatteryBuddy.Devices.Retry;

namespace BatteryBuddy.Backends.Logitech;

/// <summary>
/// Logitech devices over HID++ 2.0: real battery percentage and charging state, straight from the device
/// (no Options+ needed; it works alongside it). Covers Bluetooth devices (their own vendor collection) and
/// devices behind a Unifying/Bolt/Lightspeed receiver (device indexes 1–6). A device without a battery
/// feature, or that stops answering, is left out, so the hub falls back to other sources and inference.
/// </summary>
public sealed class LogitechSource : IDeviceSource, IConnectionObserver, IHostSwitcher, IDisposable
{
    public const ushort VendorId = 0x046D;
    const ushort BleUsagePage = 0xFF43;

    static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    static readonly TimeSpan FollowUpScan = TimeSpan.FromSeconds(5);
    // The first request on a just-woken BLE link can take several hundred ms.
    static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(1);
    static readonly byte[] DirectOnly = { HidppProtocol.DirectIndex };
    static readonly byte[] DirectOrReceiver = { HidppProtocol.DirectIndex, 1, 2, 3, 4, 5, 6 };
    static readonly Regex BleAddress = new(@"pid&[0-9a-f]{4}_rev&[0-9a-f]{4}_([0-9a-f]{12})", RegexOptions.IgnoreCase);
    // Channel switching is proven on these (Bluetooth product ids); other mice with CHANGE_HOST are tried.
    static readonly HashSet<ushort> VerifiedSwitchers = new() { 0xB034 };   // MX Master 3S

    // ChangeHost / HostsInfo: feature indexes, 0 when the device doesn't have them (or didn't answer).
    sealed record Device(HidChannel Channel, byte Index, byte BatteryFeature, bool Unified, DeviceReading Reading,
        byte ChangeHost = 0, byte HostsInfo = 0);

    readonly Action<string> _log;
    readonly bool _verbose;
    readonly RetryScheduler _wake = new(new Backoff(PollInterval, PollInterval), TimeSpan.Zero);
    readonly object _gate = new();
    readonly Dictionary<string, HidChannel> _channels = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<(string Path, byte Index), Device> _devices = new();
    // Answered HID++ but has no battery feature: don't re-probe until its collection comes back.
    readonly HashSet<(string Path, byte Index)> _unsupported = new();
    bool _disposed;

    public LogitechSource(Action<string> log, bool verbose = false)
    {
        _log = log;
        _verbose = verbose;
    }

    public string Name => "logitech";

    public event EventHandler<IReadOnlyList<DeviceReading>>? SnapshotChanged;

    public Task StartAsync(CancellationToken ct)
    {
        _ = Task.Run(() => RunAsync(ct), ct);
        return Task.CompletedTask;
    }

    /// <summary>Devices push status changes; a refresh rescans for new collections and re-reads every battery now.</summary>
    public Task RefreshAsync(CancellationToken ct)
    {
        _wake.Wake();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Something connected (Windows saw it): its HID++ collection may have just appeared, or may appear a moment
    /// after Windows reports the device, so look now and again shortly.
    /// </summary>
    public void OnDeviceConnected(DeviceReading device)
    {
        _wake.Wake();
        _ = Task.Delay(FollowUpScan).ContinueWith(_ => { if (!_disposed) _wake.Wake(); }, TaskScheduler.Default);
    }

    /// <summary>The Bluetooth address in a BLE HID path (…_pid&amp;b034_rev&amp;0006_df43c7bc3a2b&amp;col02…).</summary>
    public static string? AddressFromPath(string path) =>
        BleAddress.Match(path) is { Success: true } m ? m.Groups[1].Value.ToUpperInvariant() : null;

    async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && !_disposed)
        {
            try { await ScanAsync(ct); }
            catch (Exception ex) when (!ct.IsCancellationRequested) { _log($"logitech: scan failed: {ex.Message}"); }
            try { await _wake.WaitAsync(ct); }
            catch (OperationCanceledException) { }
        }
    }

    async Task ScanAsync(CancellationToken ct)
    {
        var found = HidInterface.Enumerate(VendorId)
            .Where(i => i.OutputLength >= HidppProtocol.LongLength && i.UsagePage is BleUsagePage or 0xFF00)
            .ToList();
        var paths = found.Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool closed = false;
        lock (_gate)
            foreach (var (path, channel) in _channels.ToList())
                if (!channel.IsAlive || !paths.Contains(path))
                {
                    Close(path, channel);
                    closed = true;
                }
        if (closed) Publish();   // don't make a disconnect wait for the probes below

        foreach (var iface in found)
        {
            lock (_gate) if (_channels.ContainsKey(iface.Path)) continue;
            if (HidChannel.Open(iface) is not { } opened) continue;
            opened.Report += report => OnReport(opened, report);
            // A dead link (device off, out of range, switched host) drops its devices now, not at the next poll.
            opened.Closed += () => { if (!_disposed) _wake.Wake(); };
            lock (_gate) _channels[iface.Path] = opened;
            if (_verbose) _log($"logitech: opened {iface.UsagePage:X4}:{iface.Usage:X4} \"{iface.Product}\" {iface.Path}");
        }

        List<HidChannel> channels;
        lock (_gate) channels = _channels.Values.ToList();
        // Every collection and receiver slot at once: a slot with nothing paired costs a full timeout.
        await Task.WhenAll(channels.SelectMany(channel =>
            (channel.Interface.UsagePage == BleUsagePage ? DirectOnly : DirectOrReceiver)
                .Select(index => ScanSlotAsync(channel, index, ct))));
        Publish();
    }

    async Task ScanSlotAsync(HidChannel channel, byte index, CancellationToken ct)
    {
        var id = (channel.Interface.Path, index);
        Device? known;
        lock (_gate)
        {
            if (_unsupported.Contains(id)) return;
            known = _devices.GetValueOrDefault(id);
        }
        var device = known is not null ? await PollAsync(known, ct) : await ProbeAsync(channel, index, ct);
        lock (_gate)
        {
            if (device is not null) _devices[id] = device;
            else if (known is not null && _devices.Remove(id))
                _log($"logitech: {known.Reading.Name} stopped answering");
        }
    }

    void Close(string path, HidChannel channel)
    {
        string why = channel.IsAlive ? "collection removed" : "link closed";
        channel.Dispose();
        _channels.Remove(path);
        foreach (var (id, device) in _devices.Where(d => d.Key.Path == path).ToList())
        {
            _devices.Remove(id);
            _log($"logitech: {device.Reading.Name} gone ({why})");
        }
        _unsupported.RemoveWhere(k => k.Path == path);
    }

    async Task<Device?> ProbeAsync(HidChannel channel, byte index, CancellationToken ct)
    {
        // A null lookup means nothing answered at this index (not paired, asleep, not HID++ 2.0): try again next scan.
        // Direct devices get a second try, since a just-woken BLE link can miss the first request.
        var first = await FeatureIndexAsync(channel, index, HidppProtocol.UnifiedBattery, ct);
        if (first is null && index == HidppProtocol.DirectIndex && channel.Interface.UsagePage == BleUsagePage)
            first = await FeatureIndexAsync(channel, index, HidppProtocol.UnifiedBattery, ct);
        if (first is not byte unified) return null;
        bool isUnified = unified != 0;
        byte feature = unified;
        if (!isUnified)
        {
            if (await FeatureIndexAsync(channel, index, HidppProtocol.BatteryStatus, ct) is not byte status) return null;
            feature = status;
        }
        if (feature == 0)
        {
            lock (_gate) _unsupported.Add((channel.Interface.Path, index));
            _log($"logitech: \"{channel.Interface.Product}\" #{index:X2} has no HID++ battery feature, leaving it to Windows");
            return null;
        }

        var (name, kind) = await DescribeAsync(channel, index, ct);
        var address = index == HidppProtocol.DirectIndex && channel.Interface.UsagePage == BleUsagePage
            ? AddressFromPath(channel.Interface.Path) : null;
        var reading = new DeviceReading(DeviceKey.ForName(name), name, kind, true, null, null, DateTimeOffset.Now,
            Name, ChargingKnown: true, Address: address);
        var (changeHost, hostsInfo) = kind == DeviceKind.Mouse ? await HostFeaturesAsync(channel, index, ct) : ((byte)0, (byte)0);
        var device = await PollAsync(new Device(channel, index, feature, isUnified, reading, changeHost, hostsInfo), ct);
        if (device is not null)
            _log($"logitech: {name} via {(isUnified ? "UNIFIED_BATTERY" : "BATTERY_STATUS")} " +
                 $"at {device.Reading.BatteryPercent}%{(device.Reading.IsCharging ? ", charging" : "")}" +
                 $"{(changeHost != 0 ? $", can switch channels{(Verified(device) ? "" : " (untested model)")}" : "")}");
        return device;
    }

    async Task<Device?> PollAsync(Device device, CancellationToken ct)
    {
        // UNIFIED_BATTERY get_status is function 1; BATTERY_STATUS GetBatteryLevelStatus is function 0.
        var reply = await device.Channel.RequestAsync(
            HidppProtocol.Request(device.Index, device.BatteryFeature, (byte)(device.Unified ? 1 : 0)), RequestTimeout, ct);
        if (_verbose) _log($"logitech: {device.Reading.Name} status -> {(reply is null ? "no reply" : Convert.ToHexString(reply))}");
        return reply is null ? null : Update(device, HidppProtocol.Params(reply));
    }

    Device? Update(Device device, ReadOnlySpan<byte> status)
    {
        var state = device.Unified ? HidppBattery.ParseUnified(status) : HidppBattery.ParseStatus(status);
        if (state is null) return null;
        return device with
        {
            Reading = device.Reading with { BatteryPercent = state.Percent, IsCharging = state.Charging, ReadAt = DateTimeOffset.Now },
        };
    }

    async Task<byte?> FeatureIndexAsync(HidChannel channel, byte index, ushort feature, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var reply = await channel.RequestAsync(HidppProtocol.FeatureLookup(index, feature), RequestTimeout, ct);
        if (_verbose)
            _log($"logitech: #{index:X2} lookup 0x{feature:X4} -> {(reply is null ? "no reply" : $"index 0x{reply[4]:X2}")} " +
                 $"in {(DateTime.UtcNow - started).TotalMilliseconds:F0} ms ({channel.Interface.UsagePage:X4})");
        return reply is null ? null : HidppProtocol.Params(reply)[0];
    }

    /// <summary>DEVICE_NAME (0x0005) name and type; falls back to the HID product string.</summary>
    async Task<(string Name, DeviceKind Kind)> DescribeAsync(HidChannel channel, byte index, CancellationToken ct)
    {
        string name = channel.Interface.Product is { Length: > 0 } product ? product : "Logitech device";
        DeviceKind? kind = null;
        if (await FeatureIndexAsync(channel, index, HidppProtocol.DeviceName, ct) is byte feature and not 0)
        {
            var count = await channel.RequestAsync(HidppProtocol.Request(index, feature, 0), RequestTimeout, ct);
            int length = count is null ? 0 : HidppProtocol.Params(count)[0];
            var text = new StringBuilder();
            while (text.Length < length)
            {
                var chunk = await channel.RequestAsync(HidppProtocol.Request(index, feature, 1, (byte)text.Length), RequestTimeout, ct);
                if (chunk is null) break;
                var chars = HidppProtocol.Params(chunk).ToArray();
                int take = Math.Min(length - text.Length, chars.Length);
                if (take <= 0) break;
                text.Append(Encoding.UTF8.GetString(chars, 0, take));
            }
            if (text.ToString().TrimEnd('\0').Trim() is { Length: > 0 } reported) name = reported;

            var type = await channel.RequestAsync(HidppProtocol.Request(index, feature, 2), RequestTimeout, ct);
            if (type is not null) kind = HidppBattery.KindFromType(HidppProtocol.Params(type)[0]);
        }
        return (name, kind ?? DeviceKindClassifier.Classify(name, null));
    }

    public static bool IsVerifiedModel(ushort productId, bool directBluetooth) =>
        directBluetooth && VerifiedSwitchers.Contains(productId);

    static bool Verified(Device device) => IsVerifiedModel(device.Channel.Interface.ProductId,
        device.Index == HidppProtocol.DirectIndex && device.Channel.Interface.UsagePage == BleUsagePage);

    /// <summary>CHANGE_HOST and HOSTS_INFO indexes (0 = absent). CHANGE_HOST only counts if getHostInfo answers sensibly.</summary>
    async Task<(byte ChangeHost, byte HostsInfo)> HostFeaturesAsync(HidChannel channel, byte index, CancellationToken ct)
    {
        if (await FeatureIndexAsync(channel, index, HidppProtocol.ChangeHost, ct) is not byte changeHost || changeHost == 0) return (0, 0);
        var info = await channel.RequestAsync(HidppHosts.GetHostInfo(index, changeHost), RequestTimeout, ct);
        if (info is null || HidppHosts.ParseHostInfo(HidppProtocol.Params(info)) is null) return (0, 0);
        byte hostsInfo = await FeatureIndexAsync(channel, index, HidppProtocol.HostsInfo, ct) ?? 0;
        return (changeHost, hostsInfo);
    }

    // The hub hands us its merged reading: match ours by key, or by Bluetooth address when names differ.
    Device? Find(DeviceReading reading)
    {
        lock (_gate)
            return _devices.Values.FirstOrDefault(d => d.Reading.Key == reading.Key ||
                reading.Address is not null && string.Equals(d.Reading.Address, reading.Address, StringComparison.OrdinalIgnoreCase));
    }

    // A verified model stays switchable even if it was silent when it connected (a stale link after sleep).
    public bool CanSwitchHost(DeviceReading device) => Find(device) is { } d && (d.ChangeHost != 0 || Verified(d));

    public bool IsVerifiedSwitcher(DeviceReading device) => Find(device) is { } d && Verified(d);

    public async Task<HostChannels?> GetHostsAsync(DeviceReading reading, CancellationToken ct)
    {
        if (Find(reading) is not { } device) return null;
        if (device.ChangeHost == 0)
        {
            var (changeHost, hostsInfo) = await HostFeaturesAsync(device.Channel, device.Index, ct);
            if (changeHost == 0)
            {
                _log($"logitech: {device.Reading.Name} isn't answering CHANGE_HOST");
                return null;
            }
            device = device with { ChangeHost = changeHost, HostsInfo = hostsInfo };
            var id = (device.Channel.Interface.Path, device.Index);
            lock (_gate)
                if (_devices.TryGetValue(id, out var stored)) _devices[id] = stored with { ChangeHost = changeHost, HostsInfo = hostsInfo };
        }
        var hosts = await HidppHosts.ReadAsync(device.Channel.RequestAsync, device.Index, device.ChangeHost, device.HostsInfo, RequestTimeout, ct);
        _log($"logitech: {device.Reading.Name} channels -> {(hosts is null ? "no reply" : Describe(hosts))}" +
             $"{(Verified(device) ? "" : " (untested model)")}");
        return hosts;
    }

    public async Task<SwitchResult> SwitchHostAsync(DeviceReading reading, int channel, CancellationToken ct)
    {
        if (Find(reading) is not { ChangeHost: not 0 } device) return SwitchResult.Failed;
        var result = await HidppHosts.SwitchAsync(device.Channel.RequestAsync, device.Index, device.ChangeHost, channel, Task.Delay, ct);
        _log($"logitech: {device.Reading.Name} switch to channel {channel} -> {result}{(Verified(device) ? "" : " (untested model)")}");
        return result;
    }

    // "1* 2 3(empty)": * marks the current channel.
    static string Describe(HostChannels hosts) => string.Join(" ", hosts.Channels.Select(c =>
        $"{c.Number}{(c.Number == hosts.Current ? "*" : c.Paired ? "" : "(empty)")}"));

    void OnReport(HidChannel channel, byte[] report)
    {
        if (HidppProtocol.IsConnectionNotice(report))
        {
            _wake.Wake();
            return;
        }
        bool changed = false;
        lock (_gate)
            foreach (var (id, device) in _devices.ToList())
            {
                if (device.Channel != channel || !HidppProtocol.IsEvent(report, device.Index, device.BatteryFeature)) continue;
                if (Update(device, HidppProtocol.Params(report)) is not { } next) continue;
                _devices[id] = next;
                changed = true;
                if (_verbose) _log($"logitech: event {next.Reading.Name} {next.Reading.BatteryPercent}% charging={next.Reading.IsCharging}");
            }
        if (changed) Publish();
    }

    void Publish()
    {
        List<DeviceReading> snapshot;
        lock (_gate) snapshot = _devices.Values.Select(d => d.Reading).ToList();
        SnapshotChanged?.Invoke(this, snapshot);
    }

    public void Dispose()
    {
        _disposed = true;
        _wake.Wake();
        lock (_gate)
            foreach (var (path, channel) in _channels.ToList()) Close(path, channel);
    }
}
