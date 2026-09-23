using System.Diagnostics;
using BatteryBuddy.Bluetooth;

var source = new WindowsBatterySource(TimeSpan.FromMinutes(5), m => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {m}"), verbose: true);
source.SnapshotChanged += (_, snapshot) =>
{
    Console.WriteLine($"--- {DateTime.Now:HH:mm:ss} snapshot ({snapshot.Count} devices)");
    foreach (var r in snapshot)
        Console.WriteLine($"    {r.Name,-28} {r.Kind,-9} {(r.IsConnected ? "connected" : "-"),-10} {r.BatteryPercent?.ToString() ?? "?"}%");
};

var clock = Stopwatch.StartNew();
await source.StartAsync(CancellationToken.None);
Console.WriteLine($"Started in {clock.ElapsedMilliseconds} ms. Turn a device off/on to see events. Press Enter to quit.");
Console.ReadLine();
source.Dispose();
