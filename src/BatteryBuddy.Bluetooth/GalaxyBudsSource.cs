using System.Diagnostics;
using BatteryBuddy.Core.Devices;
using BatteryBuddy.Core.Retry;
using BatteryBuddy.Core.Samsung;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace BatteryBuddy.Bluetooth;

public sealed class GalaxyBudsSource : IDeviceSource, IDisposable
{
    public static readonly Guid ServiceUuid = new("2e73a4ad-332d-41fc-90e2-16bef06523f2");

    readonly Action<string> _log;
    readonly bool _verbose;
    // Backoff only resets after a link that stayed up 30 s, so a takeover by the Galaxy Buds app
    // doesn't turn into a reconnect fight every 10 s.
    readonly RetryScheduler _retry = new(new Backoff(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5)), TimeSpan.FromSeconds(30));
    readonly FailureCounter _failures = new(5, TimeSpan.FromSeconds(30));
    readonly Stopwatch _clock = Stopwatch.StartNew();
    string _deviceName = "Galaxy Buds";

    public GalaxyBudsSource(Action<string> log, bool verbose = false)
    {
        _log = log;
        _verbose = verbose;
    }

    public string Name => "galaxy-buds";

    public event EventHandler<IReadOnlyList<DeviceReading>>? SnapshotChanged;

    public Task StartAsync(CancellationToken ct)
    {
        _ = Task.Run(() => RunAsync(ct), ct);
        return Task.CompletedTask;
    }

    /// <summary>Buds push their status; a refresh only cuts a pending retry delay short.</summary>
    public Task RefreshAsync(CancellationToken ct)
    {
        _retry.Wake();
        return Task.CompletedTask;
    }

    /// <summary>Windows saw the buds connect: retry now and forget any long backoff.</summary>
    public void NotifyWindowsConnection(bool connected)
    {
        if (connected) _retry.Wake(resetBackoff: true);
    }

    async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (await RunSessionAsync(ct) is TimeSpan linkDuration) _retry.SessionEnded(linkDuration);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _log($"buds: {ex.GetType().Name}: {ex.Message}");
            }
            SnapshotChanged?.Invoke(this, Array.Empty<DeviceReading>());

            try { await _retry.WaitAsync(ct); }
            catch (OperationCanceledException) { }
        }
    }

    /// <returns>How long the link stayed open, or null if no link was opened.</returns>
    async Task<TimeSpan?> RunSessionAsync(CancellationToken ct)
    {
        using var device = await FindConnectedBudsAsync(ct);
        if (device is null) return null;
        _deviceName = device.Name;

        var services = await device.GetRfcommServicesForIdAsync(
            RfcommServiceId.FromUuid(ServiceUuid), BluetoothCacheMode.Uncached).AsTask(ct);
        var service = services.Services.FirstOrDefault();
        if (service is null) return null;

        using var socket = new StreamSocket();
        await socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName).AsTask(ct);
        var openedAt = _clock.Elapsed;
        _log($"buds: link open to {_deviceName}");
        try { await ReadLinkAsync(socket, ct); }
        finally { _log("buds: link closed"); }
        return _clock.Elapsed - openedAt;
    }

    /// <summary>
    /// Any paired device named "…Buds…" may be an old or other-brand pair; take the first one that is
    /// actually connected. Never open RFCOMM to disconnected buds: it could pull them away from the phone.
    /// </summary>
    async Task<BluetoothDevice?> FindConnectedBudsAsync(CancellationToken ct)
    {
        var paired = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true)).AsTask(ct);
        foreach (var info in paired.Where(d => SamsungBuds.IsGalaxyBudsName(d.Name)))
        {
            var device = await BluetoothDevice.FromIdAsync(info.Id).AsTask(ct);
            if (device?.ConnectionStatus == BluetoothConnectionStatus.Connected) return device;
            device?.Dispose();
        }
        return null;
    }

    async Task ReadLinkAsync(StreamSocket socket, CancellationToken ct)
    {

        var decoder = new FrameDecoder();
        using var reader = new DataReader(socket.InputStream) { InputStreamOptions = InputStreamOptions.Partial };
        using var writer = new DataWriter(socket.OutputStream);
        int rejectedSeen = 0;

        while (!ct.IsCancellationRequested)
        {
            uint count = await reader.LoadAsync(1024).AsTask(ct);
            if (count == 0) return;
            var chunk = new byte[count];
            reader.ReadBytes(chunk);

            foreach (var frame in decoder.Push(chunk))
            {
                if (_verbose) _log($"buds: rx 0x{frame.MessageId:X2} {Convert.ToHexString(frame.Payload)}");
                if (frame.MessageId == StatusParser.ExtendedStatusUpdated)
                {
                    writer.WriteBytes(FrameEncoder.ExtendedStatusAck());
                    await writer.StoreAsync().AsTask(ct);
                }
                if (StatusParser.TryParse(frame) is { } detail)
                    SnapshotChanged?.Invoke(this, new[] { ToReading(detail) });
            }

            for (; rejectedSeen < decoder.RejectedFrames; rejectedSeen++)
            {
                if (!_failures.Record(_clock.Elapsed)) continue;
                _log("buds: too many bad frames, resetting link");
                return;
            }
        }
    }

    DeviceReading ToReading(BudsDetail detail) =>
        new(DeviceKey.ForName(_deviceName), _deviceName, DeviceKind.Earbuds, true,
            detail.Lowest, detail, DateTimeOffset.Now, Name);

    public void Dispose() => _retry.Wake();
}
