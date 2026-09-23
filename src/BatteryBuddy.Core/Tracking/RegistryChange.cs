using BatteryBuddy.Core.Devices;

namespace BatteryBuddy.Core.Tracking;

public sealed record RegistryChange(
    IReadOnlyList<DeviceReading> Added,
    IReadOnlyList<DeviceReading> Updated,
    IReadOnlyList<DeviceReading> Removed)
{
    public bool IsEmpty => Added.Count == 0 && Updated.Count == 0 && Removed.Count == 0;
}
