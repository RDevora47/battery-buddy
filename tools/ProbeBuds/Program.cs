using BatteryBuddy.Backends.Windows;
using BatteryBuddy.Backends.GalaxyBuds;

void Log(string message) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {message}");

using var radio = new BluetoothRadioMonitor();
radio.AvailabilityChanged += (_, on) => Log($"radio: bluetooth {(on ? "on" : "off")}");
await radio.StartAsync();

using var buds = new GalaxyBudsSource(Log, verbose: true);
buds.SnapshotChanged += (_, snapshot) =>
{
    if (snapshot.Count == 0) { Log("link down"); return; }
    var d = snapshot[0].Detail!;
    Log($"{snapshot[0].Name}: L {d.Left}% R {d.Right}% case {d.Case?.ToString() ?? "?"}% worn {d.LeftWorn}/{d.RightWorn}");
};
await buds.StartAsync(CancellationToken.None);
Log("Probing. Press Enter to quit.");
Console.ReadLine();
