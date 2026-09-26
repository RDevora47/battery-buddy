using System.Runtime.InteropServices;
using BatteryBuddy.Devices;

namespace BatteryBuddy.Backends.Windows.Audio;

/// <summary>
/// Connects paired Bluetooth earbuds or a headset the way the Windows Settings "Connect" button does. Each Bluetooth
/// audio endpoint (even an unplugged one) is wired, through its topology, to the Bluetooth audio driver's device.
/// Asking that device for KSPROPERTY_ONESHOT_RECONNECT makes the driver page the earbuds.
/// </summary>
public sealed class BluetoothAudioConnector : IDeviceConnector
{
    readonly Action<string> _log;

    public BluetoothAudioConnector(Action<string> log) => _log = log;

    // Off the UI thread: Core Audio calls can block, and the thread pool's MTA suits them.
    public Task<ConnectResult> ConnectAsync(RememberedDevice device, CancellationToken ct) => Task.Run(() => Connect(device), ct);

    ConnectResult Connect(RememberedDevice device)
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        Check(enumerator.EnumAudioEndpoints(CoreAudio.DataFlowAll, CoreAudio.DeviceStateAll, out var endpoints));
        Check(endpoints.GetCount(out uint count));

        bool found = false;
        for (uint i = 0; i < count; i++)
        {
            if (endpoints.Item(i, out var endpoint) < 0) continue;
            if (endpoint.OpenPropertyStore(CoreAudio.StgmRead, out var store) < 0) continue;
            var name = CoreAudio.ReadString(store, CoreAudio.DeviceFriendlyName);
            if (name is null || !device.Matches(name)) continue;

            found = true;
            // Headphones and headset endpoints lead to the same device: the first one that takes the request is enough.
            int hr = RequestReconnect(enumerator, endpoint);
            _log($"connect: asked {name} to reconnect: 0x{hr:X8}");
            if (hr >= 0) return ConnectResult.Requested;
        }
        if (!found) _log($"connect: no audio endpoint named like {device.Name}");
        return found ? ConnectResult.Failed : ConnectResult.NotFound;
    }

    static int RequestReconnect(IMMDeviceEnumerator enumerator, IMMDevice endpoint)
    {
        var topologyId = CoreAudio.IDeviceTopologyId;
        int hr = endpoint.Activate(ref topologyId, CoreAudio.ClsCtxAll, IntPtr.Zero, out var topology);
        if (hr < 0) return hr;
        if ((hr = ((IDeviceTopology)topology).GetConnector(0, out var connector)) < 0) return hr;
        if ((hr = connector.GetDeviceIdConnectedTo(out var driverId)) < 0) return hr;   // no driver behind this endpoint
        if ((hr = enumerator.GetDevice(driverId, out var driver)) < 0) return hr;

        var ksControlId = CoreAudio.IKsControlId;
        if ((hr = driver.Activate(ref ksControlId, CoreAudio.ClsCtxAll, IntPtr.Zero, out var ksControl)) < 0) return hr;
        var property = new KsProperty
        {
            Set = CoreAudio.KsPropSetIdBtAudio,
            Id = CoreAudio.KsPropertyOneshotReconnect,
            Flags = CoreAudio.KsPropertyTypeGet,
        };
        return ((IKsControl)ksControl).KsProperty(ref property, (uint)Marshal.SizeOf<KsProperty>(), IntPtr.Zero, 0, out _);
    }

    static void Check(int hr)
    {
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);
    }
}
