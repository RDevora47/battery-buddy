namespace BatteryBuddy.Devices;

/// <summary>
/// What a frontend consumes: one merged, de-duplicated device list in the common <see cref="DeviceReading"/>
/// format, whatever sources produce it. Events are raised on the UI thread (the synchronization context the
/// backend was given), and the members are meant to be used from that thread too.
/// </summary>
public interface IBatteryBackend : IHostSwitcher, IDisposable
{
    IReadOnlyList<DeviceReading> Connected { get; }

    /// <summary>The latest reading for a device, connected or not.</summary>
    DeviceReading? LastKnown(string key);

    /// <summary>
    /// How much use a connected device has left of what it's using up (a bud in the ear counts while its partner
    /// charges in the case); null while charging or still learning.
    /// </summary>
    TimeSpan? TimeLeft(string key, DateTimeOffset now);

    /// <summary>The PC is about to sleep.</summary>
    void Suspend();

    /// <summary>False while devices can't be reached (e.g. Bluetooth is off).</summary>
    bool IsAvailable { get; }

    event EventHandler<DeviceChange>? Changed;
    event EventHandler<bool>? AvailabilityChanged;

    Task StartAsync(CancellationToken ct);

    /// <summary>Asks every source to rescan now.</summary>
    Task RefreshAsync(CancellationToken ct);
}
