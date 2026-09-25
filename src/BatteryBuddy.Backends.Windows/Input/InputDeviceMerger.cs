using System.Globalization;
using System.Text.RegularExpressions;
using BatteryBuddy.Devices;

namespace BatteryBuddy.Backends.Windows.Input;

/// <summary>
/// Turns keyboard and mouse nodes into one battery-less reading per physical device, so the pet can show a
/// keyboard or mouse no battery source reads. Battery sources report the same device under the same name
/// (or Bluetooth address), and the hub prefers their readings. A laptop's own keyboard and touchpad show only
/// while no other keyboard or mouse is attached: the pet has one desk and one paw for them.
/// </summary>
public static class InputDeviceMerger
{
    public const string SourceName = "input";

    /// <summary>The container of the PC itself: a laptop's built-in keyboard and touchpad.</summary>
    public static readonly Guid LocalMachineContainer = new("00000000-0000-0000-ffff-ffffffffffff");

    // A Bluetooth HID collection ends in the device's address: HID\{00001812-…}_DEV_VID&…_REV&0011_FF03000652C0&COL01\…
    static readonly Regex BluetoothAddress = new(@"_([0-9A-F]{12})(&COL[0-9A-F]+)?\\", RegexOptions.IgnoreCase);
    static readonly Regex Interface = new(@"&MI_([0-9A-F]{2})", RegexOptions.IgnoreCase);
    static readonly Regex Collection = new(@"&COL([0-9A-F]{2})", RegexOptions.IgnoreCase);

    public const string BuiltInKeyboardName = "Built-in keyboard";
    public const string TouchpadName = "Touchpad";

    public static IReadOnlyList<DeviceReading> Merge(IEnumerable<InputNode> nodes, DateTimeOffset now)
    {
        var present = nodes.Where(n => n.IsPresent && n.ContainerId is not null).ToList();
        var readings = External(present.Where(n => n.ContainerId != LocalMachineContainer), now).ToList();

        // The PC's own container holds both the keyboard and the touchpad (and the keyboard's own mouse collection).
        var builtIn = present.Where(n => n.ContainerId == LocalMachineContainer).ToList();
        foreach (var (kind, name) in new[] { (DeviceKind.Keyboard, BuiltInKeyboardName), (DeviceKind.Mouse, TouchpadName) })
            if (builtIn.Any(n => n.Class == kind) && readings.All(r => r.Kind != kind))
                readings.Add(new DeviceReading(DeviceKey.ForName(name), name, kind, true, null, null, now, SourceName, NoBattery: true));
        return readings;
    }

    static IEnumerable<DeviceReading> External(IEnumerable<InputNode> nodes, DateTimeOffset now)
    {
        foreach (var device in nodes.GroupBy(n => n.ContainerId!.Value))
        {
            string name = device.Select(n => n.ContainerName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))?.Trim()
                ?? (device.First().Class == DeviceKind.Keyboard ? "Keyboard" : "Mouse");
            if (KindOf(name, device.ToList()) is not DeviceKind kind) continue;
            string? address = device.Select(n => BluetoothAddress.Match(n.InstanceId))
                                    .FirstOrDefault(m => m.Success)?.Groups[1].Value.ToUpperInvariant();
            yield return new DeviceReading(DeviceKey.ForName(name), name, kind, true, null, null, now, SourceName,
                Address: address, NoBattery: true);
        }
    }

    // Most keyboards also expose a mouse collection and most mice a keyboard one (receivers expose both), so the
    // name decides when it can. Otherwise the device's first function is what it is: the lowest interface, then
    // the lowest collection (a mouse's buttons come before its macro keys). A device the name calls something
    // else (a controller, say) isn't a keyboard or mouse at all.
    static DeviceKind? KindOf(string name, IReadOnlyList<InputNode> nodes)
    {
        var byName = DeviceKindClassifier.Classify(name, null);
        if (byName is DeviceKind.Keyboard or DeviceKind.Mouse) return byName;
        if (byName != DeviceKind.Other) return null;
        return nodes.OrderBy(n => Number(Interface, n.InstanceId))
                    .ThenBy(n => Number(Collection, n.InstanceId))
                    .ThenBy(n => n.InstanceId, StringComparer.OrdinalIgnoreCase)
                    .First().Class;
    }

    static int Number(Regex pattern, string instanceId) =>
        pattern.Match(instanceId) is { Success: true } m ? int.Parse(m.Groups[1].Value, NumberStyles.HexNumber) : 0;
}
