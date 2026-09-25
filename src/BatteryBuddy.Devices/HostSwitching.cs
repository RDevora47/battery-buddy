namespace BatteryBuddy.Devices;

/// <summary>One of a device's host slots (an Easy-Switch channel). Number is 1-based, as printed on the mouse.</summary>
public sealed record HostChannel(int Number, bool Paired);

/// <summary>A device's host slots and the one it's on now (Current is 1-based too).</summary>
public sealed record HostChannels(IReadOnlyList<HostChannel> Channels, int Current);

public enum SwitchResult { Switched, Failed }

/// <summary>
/// Optional for a source: it can move a device to another of its paired hosts (a Logitech Easy-Switch mouse).
/// Verified: switching is proven on this model; others are tried, and their failures are worded as "not supported yet".
/// </summary>
public interface IHostSwitcher
{
    bool CanSwitchHost(DeviceReading device);
    bool IsVerifiedSwitcher(DeviceReading device);

    /// <summary>Null when the device doesn't answer.</summary>
    Task<HostChannels?> GetHostsAsync(DeviceReading device, CancellationToken ct);

    Task<SwitchResult> SwitchHostAsync(DeviceReading device, int channel, CancellationToken ct);
}
