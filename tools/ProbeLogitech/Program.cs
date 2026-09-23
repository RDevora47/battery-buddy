using BatteryBuddy.Backend;
using BatteryBuddy.Backends.Logitech;
using BatteryBuddy.Backends.Logitech.Hid;
using BatteryBuddy.Backends.Logitech.Hidpp;
using BatteryBuddy.Backends.Windows;
using BatteryBuddy.Devices;

// Read-only: HID++ feature lookups and battery/name GETs only. Runs for 20 s (or pass seconds); "list" dumps collections.
void Log(string message) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {message}");

if (args.Length > 0 && args[0] == "list")
{
    foreach (var i in HidInterface.Enumerate(LogitechSource.VendorId))
        Log($"{i.UsagePage:X4}:{i.Usage:X4} pid={i.ProductId:X4} in={i.InputLength} out={i.OutputLength} \"{i.Product}\" {i.Path}");
    return;
}

if (args.Length > 0 && args[0] == "raw")
{
    // Root lookup of UNIFIED_BATTERY on every HID++ collection, printing every report that arrives.
    foreach (var iface in HidInterface.Enumerate(LogitechSource.VendorId).Where(i => i.OutputLength >= 20))
    {
        Log($"--- {iface.UsagePage:X4}:{iface.Usage:X4} {iface.Product}");
        using var channel = HidChannel.Open(iface);
        if (channel is null) { Log("cannot open"); continue; }
        channel.Report += r => Log($"  rx {Convert.ToHexString(r)}");
        await Task.Delay(200);
        Log($"  alive={channel.IsAlive}");
        foreach (byte index in iface.UsagePage == 0xFF43 ? new byte[] { 0xFF } : new byte[] { 0xFF, 1, 2 })
        {
            var reply = await channel.RequestAsync(HidppProtocol.FeatureLookup(index, HidppProtocol.UnifiedBattery), TimeSpan.FromSeconds(1), CancellationToken.None);
            Log($"  #{index:X2} reply={(reply is null ? "null" : Convert.ToHexString(reply))} alive={channel.IsAlive}");
        }
    }
    return;
}

if (args.Length > 0 && args[0] == "hub")
{
    // Windows + Logitech through the real hub, as the app composes them (Galaxy Buds left out: it would
    // take the RFCOMM link from a running app).
    using var hub = new DeviceHub(new IDeviceSource[] { new WindowsBatterySource(TimeSpan.FromMinutes(5), Log, verbose: true), new LogitechSource(Log) },
        new ChargeTracker(), Log, new BluetoothRadioMonitor());
    hub.Changed += (_, change) =>
    {
        foreach (var d in change.Added.Concat(change.Updated))
            Log($"{(change.Added.Contains(d) ? "added  " : "updated")} {d.Name} connected={d.IsConnected} {d.BatteryPercent}% charging={d.IsCharging}" +
                $" ({(d.ChargingKnown ? "reported" : "inferred")}) via {d.Source}");
        foreach (var d in change.Removed) Log($"removed {d.Name}");
    };
    hub.AvailabilityChanged += (_, on) => Log($"available={on}");
    await hub.StartAsync(CancellationToken.None);
    await Task.Delay(TimeSpan.FromSeconds(args.Length > 1 && int.TryParse(args[1], out var hs) ? hs : 15));
    Log($"connected: {string.Join(", ", hub.Connected.Select(d => $"{d.Name} {d.BatteryPercent}%"))}");
    return;
}

int seconds = args.Length > 0 && int.TryParse(args[0], out var s) ? s : 20;
using var source = new LogitechSource(Log, verbose: true);
source.SnapshotChanged += (_, snapshot) =>
{
    Log($"snapshot: {snapshot.Count} device(s)");
    foreach (var d in snapshot)
        Log($"  {d.Name} [{d.Kind}] {d.BatteryPercent}% charging={d.IsCharging} address={d.Address ?? "-"}");
};
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
await source.StartAsync(cts.Token);
try { await Task.Delay(Timeout.Infinite, cts.Token); } catch (OperationCanceledException) { }
