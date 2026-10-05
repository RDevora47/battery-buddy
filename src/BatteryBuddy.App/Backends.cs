using BatteryBuddy.Backend;
using BatteryBuddy.Backends.GalaxyBuds;
using BatteryBuddy.Backends.Logitech;
using BatteryBuddy.Backends.Windows;
using BatteryBuddy.Backends.Windows.Audio;
using BatteryBuddy.Devices;

namespace BatteryBuddy.App;

/// <summary>
/// The composition root: the only place that knows which backends exist. To add or swap a protocol,
/// implement <see cref="IDeviceSource"/> in its own project and list it here; the pet never changes.
/// </summary>
static class Backends
{
    // The watcher reports battery and connection changes, so this poll is only a backstop.
    static readonly TimeSpan WindowsPollInterval = TimeSpan.FromMinutes(5);

    /// <summary>Call on the UI thread: the hub raises its events on the context it was created on.</summary>
    public static IBatteryBackend Create()
    {
        var charge = new ChargeTracker(DeviceStatsFile.Load());
        charge.StatsChanged += () => DeviceStatsFile.Save(charge.Stats);
        var drain = new DrainHistory(UsageFile.Load());
        drain.Changed += () => UsageFile.Save(drain.All);

        IDeviceSource[] sources =
        {
            new WindowsBatterySource(WindowsPollInterval, Log.Write),
            new GalaxyBudsSource(Log.Write),
            new LogitechSource(Log.Write),
            new InputDeviceSource(Log.Write),
        };
        var hub = new DeviceHub(sources, charge, Log.Write, new BluetoothRadioMonitor(), SynchronizationContext.Current, drain);
        // Time asleep isn't use: end every drain stretch before the PC sleeps.
        Microsoft.Win32.SystemEvents.PowerModeChanged += (_, e) =>
        {
            if (e.Mode == Microsoft.Win32.PowerModes.Suspend) hub.Suspend();
        };
        return hub;
    }

    public static IDeviceConnector CreateConnector() => new BluetoothAudioConnector(Log.Write);
}
