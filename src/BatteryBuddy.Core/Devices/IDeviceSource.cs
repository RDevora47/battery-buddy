namespace BatteryBuddy.Core.Devices;

/// <summary>
/// Reports full snapshots of the devices it knows about. Events may be raised on any thread.
/// </summary>
public interface IDeviceSource
{
    string Name { get; }
    event EventHandler<IReadOnlyList<DeviceReading>>? SnapshotChanged;
    Task StartAsync(CancellationToken ct);
    Task RefreshAsync(CancellationToken ct);
}
