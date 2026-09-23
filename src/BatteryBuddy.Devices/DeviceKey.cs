namespace BatteryBuddy.Devices;

public static class DeviceKey
{
    public static string ForName(string name) =>
        string.Join(' ', name.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
