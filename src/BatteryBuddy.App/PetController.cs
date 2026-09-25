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
    SkinLayout _layout = null!;
    IReadOnlyDictionary<string, Sprite> _sprites = null!;
    PetAnimator _animator = null!;
    readonly Stopwatch _clock = Stopwatch.StartNew();
    WriteableBitmap _bitmap = null!;
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
        if (!SkinLoader.Exists(_settings.Skin)) _settings = _settings with { Skin = SkinLoader.Default };
        LoadSkin(_settings.Skin);

        _frameTimer = new DispatcherTimer { Interval = PetAnimator.FrameInterval };
        _frameTimer.Tick += (_, _) => Render();
        _idleTimer = new DispatcherTimer();
        _idleTimer.Tick += (_, _) =>
        {
            _window.KeepOnTop();
            Render();
        };
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
        _input.MouseScrolled += () => { _animator.Scroll(_clock.Elapsed); Render(); };

#if DIAG_LOG
        StartDiagnostics();
#endif
    }

#if DIAG_LOG
    // Why does the pet window sometimes go blank? A heartbeat every second into the rolling logs\diag.log.
    DispatcherTimer? _diagTimer;
    int _renders;

    void StartDiagnostics()
    {
        _diagTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _diagTimer.Tick += (_, _) =>
        {
            int opaque = _frame is null ? -1 : _frame.Pixels.Count(p => p != 0);
            DiagLog.Write($"heartbeat renders={_renders} opaque={opaque} visible={_window.IsVisible} state={_window.WindowState} " +
                $"opacity={_window.Opacity} img={_window.PetImage.ActualWidth}x{_window.PetImage.ActualHeight} imgVis={_window.PetImage.IsVisible} " +
                $"pos={_window.Left:0},{_window.Top:0} frameTimer={_frameTimer.IsEnabled} idleTimer={_idleTimer.IsEnabled} " +
                $"tier={RenderCapability.Tier >> 16} bmp={_bitmap.PixelWidth}x{_bitmap.PixelHeight} src={ReferenceEquals(_window.PetImage.Source, _bitmap)}");
            _renders = 0;
            DiagLog.Flush();
        };
        _diagTimer.Start();
        RenderCapability.TierChanged += (_, _) => DiagLog.Write($"render tier changed to {RenderCapability.Tier >> 16}");
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => DiagLog.Write("display settings changed");
        Microsoft.Win32.SystemEvents.PowerModeChanged += (_, e) => DiagLog.Write($"power mode {e.Mode}");
        Microsoft.Win32.SystemEvents.SessionSwitch += (_, e) => DiagLog.Write($"session {e.Reason}");
        _window.IsVisibleChanged += (_, _) => DiagLog.Write($"window visible={_window.IsVisible}");
    }
#endif

    public Sprite IconSprite => Stack(_sprites["body_idle"], _sprites["gills_perky"]);

    /// <summary>Raised after the pet changes, so the tray can show the new one.</summary>
    public event Action? SkinChanged;

    public string Skin
    {
        get => _settings.Skin;
        set
        {
            if (value == _settings.Skin || !SkinLoader.Exists(value)) return;
            _settings = _settings with { Skin = value };
            _settings.Save();
            LoadSkin(value);
            Rebuild();
            SkinChanged?.Invoke();
        }
    }

    // A fresh animator for the new skin's overlay spots; Rebuild gives it the current mood.
    void LoadSkin(string skin)
    {
        (_layout, _sprites) = SkinLoader.Load(skin);
        _animator = new PetAnimator(Random.Shared.NextDouble, _clock.Elapsed,
            _layout.Overlays["zzz"], _layout.Overlays["sweat"], _layout.Overlays["saiyan"], _layout.Overlays["strawhat"],
            _layout.Overlays["magnifier"])
        {
            Hat = _settings.Hat,
            Hops = _layout.Perched,
            ReachFrames = _layout.Reach is null ? 0 : Enumerable.Range(1, 9).TakeWhile(n => _sprites.ContainsKey($"reach{n}")).Count(),
        };
        int resolution = FrameComposer.ResolutionOf(_sprites);
        _bitmap = new WriteableBitmap(_layout.CanvasWidth * resolution, _layout.CanvasHeight * resolution, 96, 96, PixelFormats.Bgra32, null);
        _window.SetBitmap(_bitmap, resolution);
    }

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

    /// <summary>What the pet wears while every device is nearly full.</summary>
    public FullChargeHat Hat
    {
        get => _settings.Hat;
        set
        {
            _settings = _settings with { Hat = value };
            _settings.Save();
            _animator.Hat = value;
            Render();
        }
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
        // With Bluetooth off only devices that don't need it stay: a wired or receiver keyboard or mouse.
        IReadOnlyList<DeviceReading> connected = _bluetoothOn ? _backend.Connected : _backend.Connected.Where(d => d.NoBattery).ToList();
        _placements = SlotAssigner.Assign(connected);
        _animator.Mood = MoodCalculator.From(connected);
        _animator.HasCritical = connected.Any(d => d.EffectiveBattery <= BatteryBar.CriticalAtOrBelow);
        _animator.LowestBattery = MoodCalculator.Lowest(connected);
        _animator.SuperSaiyan = MoodCalculator.SuperSaiyan(connected);
        Render();
    }

    void Render()
    {
        var now = _clock.Elapsed;
        var spec = _animator.FrameAt(now);
        _frame = FrameComposer.Compose(_layout, _sprites, _placements, spec, _settings.BatteryStyle);
        _bitmap.WritePixels(new Int32Rect(0, 0, _frame.Width, _frame.Height), _frame.Pixels, _frame.Width * 4, 0);
#if DIAG_LOG
        _renders++;
#endif

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
        (double, double)? saved = _settings is { Left: double left, Top: double top } ? (left, top) : null;
        var (x, y) = WindowPlacement.Resolve(saved, _window.PetInWindow, _window.WorkAreas(), _window.PrimaryWorkArea());
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
#if DIAG_LOG
        _diagTimer?.Stop();
        DiagLog.Write("shutting down");
        DiagLog.Flush();
#endif
        _backend.Dispose();
    }
}
