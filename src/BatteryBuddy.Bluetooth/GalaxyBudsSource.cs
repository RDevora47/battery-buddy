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
    readonly Backoff _backoff = new(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
    readonly FailureCounter _failures = new(5, TimeSpan.FromSeconds(30));
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly object _gate = new();
    CancellationTokenSource? _wake;
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
        Wake();
        return Task.CompletedTask;
    }

    public void NotifyWindowsConnection(bool connected)
    {
        if (connected) Wake();
    }

    void Wake()
    {
        lock (_gate) _wake?.Cancel();
    }

    async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            bool hadSession = false;
            try { hadSession = await RunSessionAsync(ct); }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _log($"buds: {ex.GetType().Name}: {ex.Message}");
            }
            SnapshotChanged?.Invoke(this, Array.Empty<DeviceReading>());
            if (hadSession) _backoff.Reset();

            CancellationTokenSource wake;
            lock (_gate)
            {
                _wake?.Dispose();
                _wake = wake = CancellationTokenSource.CreateLinkedTokenSource(ct);
            }
            try { await Task.Delay(_backoff.NextDelay(), wake.Token); }
            catch (OperationCanceledException) { }
        }
    }

    /// <returns>true if a link was opened (so the backoff restarts from its initial delay).</returns>
    async Task<bool> RunSessionAsync(CancellationToken ct)
    {
        var paired = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true)).AsTask(ct);
        var info = paired.FirstOrDefault(d => SamsungBuds.IsGalaxyBudsName(d.Name));
        if (info is null) return false;

        using var device = await BluetoothDevice.FromIdAsync(info.Id).AsTask(ct);
        // Never open RFCOMM to buds that aren't connected: it could pull them away from the phone.
        if (device is null || device.ConnectionStatus != BluetoothConnectionStatus.Connected) return false;
        _deviceName = device.Name;

        var services = await device.GetRfcommServicesForIdAsync(
            RfcommServiceId.FromUuid(ServiceUuid), BluetoothCacheMode.Uncached).AsTask(ct);
        var service = services.Services.FirstOrDefault();
        if (service is null) return false;

        using var socket = new StreamSocket();
        await socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName).AsTask(ct);
        _log($"buds: link open to {_deviceName}");

        var decoder = new FrameDecoder();
        using var reader = new DataReader(socket.InputStream) { InputStreamOptions = InputStreamOptions.Partial };
        using var writer = new DataWriter(socket.OutputStream);
        int rejectedSeen = 0;

        while (!ct.IsCancellationRequested)
        {
            uint count = await reader.LoadAsync(1024).AsTask(ct);
            if (count == 0)
            {
                _log("buds: link closed");
                break;
            }
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
                return true;
            }
        }
        return true;
    }

    DeviceReading ToReading(BudsDetail detail) =>
        new(DeviceKey.ForName(_deviceName), _deviceName, DeviceKind.Earbuds, true,
            detail.Lowest, detail, DateTimeOffset.Now, Name);

    public void Dispose() => Wake();
}
