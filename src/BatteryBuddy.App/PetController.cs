using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BatteryBuddy.Devices;
using BatteryBuddy.Pet.Animation;
using BatteryBuddy.Pet.Scene;
using BatteryBuddy.Pet.Ui;

namespace BatteryBuddy.App;

/// <summary>The pet frontend. It only sees devices through <see cref="IBatteryBackend"/>, whatever produces them.</summary>
sealed class PetController : IDisposable
{
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
    readonly IBatteryBackend _backend;
    readonly InputMonitor _input = new();
    readonly CancellationTokenSource _cts = new();
    Settings _settings = Settings.Load();
    IReadOnlyList<DevicePlacement> _placements = Array.Empty<DevicePlacement>();
    ComposedFrame? _frame;
    bool _bluetoothOn = true;
    bool _rescanning;

    public PetController(PetWindow window, IBatteryBackend backend)
    {
        _window = window;
        _backend = backend;
        (_layout, _sprites) = SkinLoader.Load("axolotl");
        _animator = new PetAnimator(Random.Shared.NextDouble, _clock.Elapsed,
            _layout.Overlays["zzz"], _layout.Overlays["sweat"], _layout.Overlays["saiyan"]);
        int resolution = FrameComposer.ResolutionOf(_sprites);
        _bitmap = new WriteableBitmap(_layout.CanvasWidth * resolution, _layout.CanvasHeight * resolution, 96, 96, PixelFormats.Bgra32, null);
        _window.SetBitmap(_bitmap, resolution);

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
        _backend.Changed += OnDevicesChanged;
        _backend.AvailabilityChanged += (_, on) => OnAvailabilityChanged(on);

        _window.PetClicked += OnPetClicked;
        _window.Moved += SavePosition;
        _input.KeyPressed += () => { _animator.KeyTap(_clock.Elapsed); Render(); };
        _input.MouseClicked += () => { _animator.Click(_clock.Elapsed); Render(); };
    }

    public Sprite IconSprite => Stack(_sprites["body_idle"], _sprites["gills_perky"]);

    // Top drawn over bottom, both anchored top-left; sized to cover both.
    static Sprite Stack(Sprite bottom, Sprite top)
    {
        int w = Math.Max(bottom.Width, top.Width), h = Math.Max(bottom.Height, top.Height);
        var pixels = new uint[w * h];
        foreach (var s in new[] { bottom, top })
            for (int y = 0; y < s.Height; y++)
                for (int x = 0; x < s.Width; x++)
                    if (s.Pixels[y * s.Width + x] is var c and not 0) pixels[y * w + x] = c;
        return new Sprite(bottom.Name, w, h, pixels);
    }

    public BatteryStyle BatteryStyle
    {
        get => _settings.BatteryStyle;
        set
        {
            _settings = _settings with { BatteryStyle = value };
            _settings.Save();
            Rebuild();
        }
    }

    public async Task StartAsync()
    {
        PlaceWindow();
        _window.Show();
        _input.Start(_window);
        Render();
        await _backend.StartAsync(_cts.Token);
    }

    public async Task RescanAsync()
    {
        if (_rescanning || !_bluetoothOn) return;
        _rescanning = true;
        _animator.BeginSniff(_clock.Elapsed);
        Render();
        try
        {
            await _backend.RefreshAsync(_cts.Token);
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

    void OnDevicesChanged(object? sender, DeviceChange change)
    {
        var now = _clock.Elapsed;
        foreach (var gone in change.Removed)
        {
            var old = _placements.FirstOrDefault(p => p.Device.Key == gone.Key);
            if (old is null) continue;
            var point = _layout.Places[old.PlaceName].Points[0];
            _animator.AddPloof(point.X, point.Y, now);
        }
        Rebuild();

        foreach (var arrived in change.Added)
        {
            var placement = _placements.FirstOrDefault(p => p.Device.Key == arrived.Key);
            if (placement is null) continue;
            var point = _layout.Places[placement.PlaceName].Points[0];
            _animator.AddWhoosh(point.X, point.Y, now);
        }
        if (change.Added.Count > 0) Render();
    }

    void OnAvailabilityChanged(bool on)
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
            var latest = _backend.LastKnown(device.Key) ?? device;
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
        IReadOnlyList<DeviceReading> connected = _bluetoothOn ? _backend.Connected : Array.Empty<DeviceReading>();
        _placements = SlotAssigner.Assign(connected);
        _animator.Mood = MoodCalculator.From(connected);
        _animator.HasCritical = connected.Any(d => d.EffectiveBattery <= BatteryBar.CriticalAtOrBelow);
        _animator.LowestBattery = MoodCalculator.Lowest(connected);
        _animator.AllFull = MoodCalculator.AllFull(connected);
        Render();
    }

    void Render()
    {
        var now = _clock.Elapsed;
        var spec = _animator.FrameAt(now);
        _frame = FrameComposer.Compose(_layout, _sprites, _placements, spec, _settings.BatteryStyle);
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
        _input.Dispose();
        _frameTimer.Stop();
        _idleTimer.Stop();
        _bubbleTimer.Stop();
        _backend.Dispose();
    }
}
