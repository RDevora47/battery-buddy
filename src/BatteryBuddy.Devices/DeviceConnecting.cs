namespace BatteryBuddy.Devices;

/// <summary>
/// A paired device the user can call back on demand. Matched by name, like the device shows in Windows
/// (a name part is enough: "Buds3 Pro" matches "Buds3 Pro de Roberto").
/// </summary>
public sealed record RememberedDevice(string Name, DeviceKind Kind)
{
    public bool Matches(string name) => name.Contains(Name, StringComparison.OrdinalIgnoreCase);
}

public enum ConnectResult { Requested, NotFound, Failed }

/// <summary>Asks Windows to connect a remembered (paired, not connected) device.</summary>
public interface IDeviceConnector
{
    /// <summary>Requested: Windows took the request; the device shows up through the sources once it connects.</summary>
    Task<ConnectResult> ConnectAsync(RememberedDevice device, CancellationToken ct);
}
