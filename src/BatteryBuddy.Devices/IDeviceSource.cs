namespace BatteryBuddy.Devices;

/// <summary>
/// A backend: one protocol or OS API that finds devices and reads their batteries, reporting them as
/// <see cref="DeviceReading"/>s. Sources know nothing about each other or the UI; the hub merges them.
/// Each snapshot is the full list this source currently knows. Events may be raised on any thread.
/// </summary>
public interface IDeviceSource
{
    string Name { get; }
    event EventHandler<IReadOnlyList<DeviceReading>>? SnapshotChanged;
    Task StartAsync(CancellationToken ct);
    Task RefreshAsync(CancellationToken ct);
}

/// <summary>
/// Optional for a source: hear about devices other sources found (e.g. Windows saw the buds connect,
/// so the buds source should try its link now). Called on the hub's thread; must not block.
/// </summary>
public interface IConnectionObserver
{
    void OnDeviceConnected(DeviceReading device);
}

/// <summary>Optional for a source: it reports a <see cref="DeviceReading.Detail"/> for some devices when it can reach them.</summary>
public interface IDetailSource
{
    bool ProvidesDetailFor(DeviceReading device);
}

/// <summary>Whether devices can be reached at all (e.g. the Bluetooth radio is on).</summary>
public interface IAvailabilityMonitor
{
    /// <summary>Raised once on start, then on every change. Any thread.</summary>
    event EventHandler<bool>? AvailabilityChanged;
    Task StartAsync();
}
