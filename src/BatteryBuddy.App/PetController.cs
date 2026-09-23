using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BatteryBuddy.Bluetooth;
using BatteryBuddy.Core.Animation;
using BatteryBuddy.Core.Devices;
using BatteryBuddy.Core.Samsung;
using BatteryBuddy.Core.Scene;
using BatteryBuddy.Core.Tracking;
using BatteryBuddy.Core.Ui;

namespace BatteryBuddy.App;

sealed class PetController : IDisposable
{
    // The Task 5 probe showed the watcher reports battery and connection changes, so this is only a backstop.
    static readonly TimeSpan WindowsPollInterval = TimeSpan.FromMinutes(5);
    static readonly TimeSpan BubbleDuration = TimeSpan.FromSeconds(6);

    readonly PetWindow _window;
    readonly SkinLayout _layout;
    readonly IReadOnlyDictionary<string, Sprite> _sprites;
    readonly PetAnimator _animator;
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly WriteableBitmap _bitmap;
    readonly DispatcherTimer _frameTimer;
    readonly DispatcherTimer _idleTimer;
    readonly DispatcherTimer _bubbleTimer;
    readonly DeviceRegistry _registry = new();
    readonly WindowsBatterySource _windows;
    readonly GalaxyBudsSource _buds;
    readonly BluetoothRadioMonitor _radio = new();
    readonly CancellationTokenSource _cts = new();
    Settings _settings = Settings.Load();
    IReadOnlyList<DevicePlacement> _placements = Array.Empty<DevicePlacement>();
    ComposedFrame? _frame;
    bool _bluetoothOn = true;
    bool _rescanning;

    public PetController(PetWindow window)
    {
        _window = window;
        (_layout, _sprites) = SkinLoader.Load("axolotl");
        _animator = new PetAnimator(Random.Shared.NextDouble, _clock.Elapsed, _layout.Overlays["zzz"], _layout.Overlays["sweat"]);
        _bitmap = new WriteableBitmap(_layout.CanvasWidth, _layout.CanvasHeight, 96, 96, PixelFormats.Bgra32, null);
        _window.SetBitmap(_bitmap);

        _frameTimer = new DispatcherTimer { Interval = PetAnimator.FrameInterval };
        _frameTimer.Tick += (_, _) => Render();
        _idleTimer = new DispatcherTimer();
        _idleTimer.Tick += (_, _) => Render();
        _bubbleTimer = new DispatcherTimer { Interval = BubbleDuration };
        _bubbleTimer.Tick += (_, _) =>
        {
            _bubbleTimer.Stop();
            _window.HideBubble();
        };

        _windows = new WindowsBatterySource(WindowsPollInterval, Log.Write);
        _buds = new GalaxyBudsSource(Log.Write);
        _registry.Changed += OnRegistryChanged;
        foreach (IDeviceSource source in new IDeviceSource[] { _windows, _buds })
            source.SnapshotChanged += (_, snapshot) =>
                _window.Dispatcher.BeginInvoke(() => _registry.Apply(source.Name, snapshot));
        _radio.AvailabilityChanged += (_, on) => _window.Dispatcher.BeginInvoke(() => OnBluetoothAvailability(on));

        _window.PetClicked += OnPetClicked;
        _window.Moved += SavePosition;
    }

    public Sprite IconSprite => _sprites["body_idle"];

    public async Task StartAsync()
    {
        PlaceWindow();
        _window.Show();
        Render();
        await StartSafely("radio", _radio.StartAsync);
        await StartSafely("windows", () => _windows.StartAsync(_cts.Token));
        await StartSafely("buds", () => _buds.StartAsync(_cts.Token));
    }

    public async Task RescanAsync()
    {
        if (_rescanning || !_bluetoothOn) return;
        _rescanning = true;
        _animator.BeginSniff(_clock.Elapsed);
        Render();
        try
        {
            await Task.WhenAll(_windows.RefreshAsync(_cts.Token), _buds.RefreshAsync(_cts.Token));
        }
        catch (Exception ex) when (!_cts.IsCancellationRequested)
        {
            Log.Write($"rescan failed: {ex.Message}");
        }
        finally
        {
            _rescanning = false;
            _animator.EndSniff(_clock.Elapsed);
            Render();
        }
    }

    static async Task StartSafely(string what, Func<Task> start)
    {
        try { await start(); }
        catch (Exception ex) { Log.Write($"{what}: start failed: {ex}"); }
    }

    void OnRegistryChanged(object? sender, RegistryChange change)
    {
        var now = _clock.Elapsed;
        foreach (var gone in change.Removed)
        {
            var old = _placements.FirstOrDefault(p => p.Device.Key == gone.Key);
            if (old is null) continue;
            var point = _layout.Places[old.PlaceName].Points[0];
            _animator.AddPloof(point.X, point.Y, now);
        }
        if (change.Added.Any(d => SamsungBuds.IsGalaxyBudsName(d.Name)))
            _buds.NotifyWindowsConnection(true);
        Rebuild();
    }

    void OnBluetoothAvailability(bool on)
    {
        bool wasOn = _bluetoothOn;
        _bluetoothOn = on;
        Rebuild();
        if (!on)
        {
            _bubbleTimer.Stop();
            _window.ShowBubble(BubbleText.BluetoothOff);
        }
        else if (!wasOn)
        {
            _window.HideBubble();
            _ = RescanAsync();
        }
    }

    void OnPetClicked(int x, int y)
    {
        if (_frame is null || !_bluetoothOn) return; // keep the "Bluetooth is off" bubble up
        var target = _frame.HitTest(x, y, out var device);
        if (target == HitTarget.None) return;

        _bubbleTimer.Stop();
        _window.HideBubble();
        if (target == HitTarget.Device && device is not null)
        {
            var latest = _registry.LastKnown(device.Key) ?? device;
            _window.ShowBubble(BubbleText.For(latest, DateTimeOffset.Now));
            _bubbleTimer.Start();
        }
        else
        {
            _ = RescanAsync();
        }
    }

    void Rebuild()
    {
        IReadOnlyList<DeviceReading> connected = _bluetoothOn ? _registry.Connected : Array.Empty<DeviceReading>();
        _placements = SlotAssigner.Assign(connected);
        _animator.Mood = MoodCalculator.From(connected);
        _animator.HasCritical = connected.Any(d => d.EffectiveBattery <= BatteryBar.CriticalAtOrBelow);
        Render();
    }

    void Render()
    {
        var now = _clock.Elapsed;
        var spec = _animator.FrameAt(now);
        _frame = FrameComposer.Compose(_layout, _sprites, _placements, spec);
        _bitmap.WritePixels(new Int32Rect(0, 0, _frame.Width, _frame.Height), _frame.Pixels, _frame.Width * 4, 0);

        if (_animator.NeedsTicks(now)) { if (!_frameTimer.IsEnabled) _frameTimer.Start(); }
        else _frameTimer.Stop();

        _idleTimer.Stop();
        var untilIdle = _animator.NextIdleAt - now;
        _idleTimer.Interval = untilIdle > TimeSpan.FromMilliseconds(50) ? untilIdle : TimeSpan.FromMilliseconds(50);
        _idleTimer.Start();
    }

    void PlaceWindow()
    {
        new WindowInteropHelper(_window).EnsureHandle();
        var dpi = VisualTreeHelper.GetDpi(_window);
        ScreenRect ToDip(System.Drawing.Rectangle r) =>
            new(r.X / dpi.DpiScaleX, r.Y / dpi.DpiScaleY, r.Width / dpi.DpiScaleX, r.Height / dpi.DpiScaleY);

        var areas = System.Windows.Forms.Screen.AllScreens.Select(s => ToDip(s.WorkingArea)).ToList();
        var primary = ToDip(System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea);
        (double, double)? saved = _settings is { Left: double left, Top: double top } ? (left, top) : null;
        var (x, y) = WindowPlacement.Resolve(saved, _window.Width, _window.Height, areas, primary);
        _window.Left = x;
        _window.Top = y;
    }

    void SavePosition()
    {
        _settings = _settings with { Left = _window.Left, Top = _window.Top };
        _settings.Save();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _frameTimer.Stop();
        _idleTimer.Stop();
        _bubbleTimer.Stop();
        _windows.Dispose();
        _buds.Dispose();
        _radio.Dispose();
    }
}
