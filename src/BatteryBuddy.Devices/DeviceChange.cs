namespace BatteryBuddy.Devices;

/// <summary>What changed in the merged device list since the last event.</summary>
public sealed record DeviceChange(
    IReadOnlyList<DeviceReading> Added,
    IReadOnlyList<DeviceReading> Updated,
    IReadOnlyList<DeviceReading> Removed)
{
    public static readonly DeviceChange None = new(Array.Empty<DeviceReading>(), Array.Empty<DeviceReading>(), Array.Empty<DeviceReading>());

    public bool IsEmpty => Added.Count == 0 && Updated.Count == 0 && Removed.Count == 0;
}
