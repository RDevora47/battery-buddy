using BatteryBuddy.Core.Devices;

namespace BatteryBuddy.Core.Scene;

public sealed record DevicePlacement(DeviceReading Device, string PlaceName, string SpriteName);

public static class SlotAssigner
{
    static readonly string[] FloatPlaces = { "float1", "float2" };

    public static IReadOnlyList<DevicePlacement> Assign(IReadOnlyList<DeviceReading> devices)
    {
        var connected = devices.Where(d => d.IsConnected).OrderBy(d => d.Key, StringComparer.Ordinal).ToList();
        var used = new HashSet<string>();
        var placements = new List<DevicePlacement>();

        foreach (var device in connected)
        {
            string[] preferred = device.Kind switch
            {
                DeviceKind.Earbuds => new[] { "gills", "neck" },
                DeviceKind.Mouse => new[] { "righthand" },
                DeviceKind.Gamepad => new[] { "hands" },
                DeviceKind.Keyboard => new[] { "seat" },
                DeviceKind.Phone => new[] { "side" },
                _ => Array.Empty<string>(),
            };
            // HashSet.Add returns false for taken places, so this picks the first free one.
            string? place = preferred.Concat(FloatPlaces).FirstOrDefault(used.Add);
            if (place is null) continue;

            string sprite = FloatPlaces.Contains(place) ? "gadget" : SpriteFor(device.Kind);
            placements.Add(new DevicePlacement(device, place, sprite));
        }
        return placements;
    }

    static string SpriteFor(DeviceKind kind) => kind switch
    {
        DeviceKind.Earbuds => "earbud",
        DeviceKind.Mouse => "mouse",
        DeviceKind.Gamepad => "gamepad",
        DeviceKind.Keyboard => "keyboard",
        DeviceKind.Phone => "phone",
        _ => "gadget",
    };
}
