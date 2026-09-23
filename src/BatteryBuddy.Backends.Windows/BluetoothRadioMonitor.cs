using BatteryBuddy.Devices;
using Windows.Devices.Radios;

namespace BatteryBuddy.Backends.Windows;

public sealed class BluetoothRadioMonitor : IAvailabilityMonitor, IDisposable
{
    Radio? _radio;

    /// <summary>true = Bluetooth on. Raised once on start, then on every change. Any thread.</summary>
    public event EventHandler<bool>? AvailabilityChanged;

    public async Task StartAsync()
    {
        var radios = await Radio.GetRadiosAsync();
        _radio = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
        if (_radio is null)
        {
            AvailabilityChanged?.Invoke(this, false);
            return;
        }
        _radio.StateChanged += OnStateChanged;
        AvailabilityChanged?.Invoke(this, _radio.State == RadioState.On);
    }

    void OnStateChanged(Radio sender, object args) =>
        AvailabilityChanged?.Invoke(this, sender.State == RadioState.On);

    public void Dispose()
    {
        if (_radio is not null) _radio.StateChanged -= OnStateChanged;
    }
}
