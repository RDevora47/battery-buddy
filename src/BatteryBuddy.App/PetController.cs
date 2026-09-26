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

    // Hidden for now: a triple-click on the pet calls these back. Later: a list the user edits, and a visible way in.
    static readonly RememberedDevice[] Remembered = { new("Buds3 Pro", DeviceKind.Earbuds) };

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
    readonly IDeviceConnector _connector;
    readonly ClickStreak _tripleClick = new(3, TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime));
    bool _connecting;
    readonly InputMonitor _input = new();
    readonly CancellationTokenSource _cts = new();
    Settings _settings = Settings.Load();
    IReadOnlyList<DevicePlacement> _placements = Array.Empty<DevicePlacement>();
    ComposedFrame? _frame;
    bool _bluetoothOn = true;
    bool _rescanning;
    HostPicker _picker = null!;          // per skin: its frame width places the arc
    DeviceReading? _pickerMouse;         // the mouse the open picker switches
    bool _pickerVerified;
    bool _openingPicker;                 // asking the mouse for its channels

    public PetController(PetWindow window, IBatteryBackend backend, IDeviceConnector connector)
    {
        _window = window;
        _backend = backend;
        _connector = connector;
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
        _window.PetRightClicked += (x, y) => _ = OnPetRightClickedAsync(x, y);
        _window.PetHovered += OnPetHovered;
        _window.Moved += ClosePicker;
        _input.EscapePressed += ClosePicker;
        // A click anywhere off the pet closes the picker; clicks on it are handled by OnPetClicked.
        _input.MouseClicked += () => { if (_picker.IsOpen && !_window.IsMouseOver) ClosePicker(); };
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
            Hops = _layout.Hops,
            ReachFrames = _layout.Reach is null ? 0 : Enumerable.Range(1, 9).TakeWhile(n => _sprites.ContainsKey($"reach{n}")).Count(),
        };
        _picker = new HostPicker(_layout.FrameWidth);
        int resolution = FrameComposer.ResolutionOf(_sprites);
        _bitmap = new WriteableBitmap(_layout.FrameWidth * resolution, _layout.CanvasHeight * resolution, 96, 96, PixelFormats.Bgra32, null);
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
        // The mouse left while its picker was out (not because we switched it): drop the picker.
        if (_picker.DeviceKey is { } pickerKey && _picker.Phase != PickerPhase.Leaving && change.Removed.Any(d => d.Key == pickerKey))
        {
            _picker.Reset();
            _window.SetHandCursor(false);
        }
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
        if (_frame is null) return;
        if (_picker.IsOpen)
        {
            if (_frame.HitTest(x, y, out _, out int channel) == HitTarget.Channel) _ = PickAsync(channel);
            else ClosePicker();
            return;
        }
        if (!_bluetoothOn) return; // keep the "Bluetooth is off" bubble up
        var target = _frame.HitTest(x, y, out var device);
        if (target == HitTarget.None) return;
        if (target != HitTarget.Body) _tripleClick.Reset();
        else if (_tripleClick.Register(_clock.Elapsed))
        {
            _ = ConnectRememberedAsync();
            return;
        }

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

    async Task OnPetRightClickedAsync(int x, int y)
    {
        if (_frame is null) return;
        var target = _frame.HitTest(x, y, out var device, out _);
        if (target == HitTarget.Channel) return;
        if (target != HitTarget.Device || device is not { Kind: DeviceKind.Mouse })
        {
            ClosePicker();
            _window.RequestMenu();
            return;
        }
        if (_picker.IsOpen)
        {
            ClosePicker();   // right-clicking the mouse again
            return;
        }
        if (_openingPicker || _picker.IsSwitching) return;   // still asking the mouse, or still confirming a switch
        _openingPicker = true;
        try { await OpenPickerAsync(device); }
        catch (Exception ex) when (!_cts.IsCancellationRequested) { Log.Write($"channel picker failed: {ex.Message}"); }
        finally { _openingPicker = false; }
    }

    async Task OpenPickerAsync(DeviceReading mouse)
    {
        if (!_backend.CanSwitchHost(mouse))
        {
            Say(SwitchText.CantSwitch);
            return;
        }
        bool verified = _backend.IsVerifiedSwitcher(mouse);
        var hosts = await _backend.GetHostsAsync(mouse, _cts.Token);
        if (hosts is null)
        {
            Say(SwitchText.NoAnswer(verified));
            return;
        }
        if (_placements.FirstOrDefault(p => p.Device.Key == mouse.Key) is not { } placement) return;   // left while we asked
        _bubbleTimer.Stop();
        _window.HideBubble();
        _pickerMouse = mouse;
        _pickerVerified = verified;
        _picker.Open(mouse.Key, _layout.Places[placement.PlaceName].Points[0], hosts, _clock.Elapsed);
        Render();
    }

    async Task PickAsync(int channel)
    {
        if (!_picker.CanPick(channel) || _pickerMouse is not { } mouse) return;
        _picker.Pick(channel, _clock.Elapsed);
        _window.SetHandCursor(false);
        Render();
        try
        {
            var result = await _backend.SwitchHostAsync(mouse, channel, _cts.Token);
            Say(result == SwitchResult.Switched ? SwitchText.Switched(channel) : SwitchText.SwitchFailed(_pickerVerified));
        }
        catch (Exception ex) when (!_cts.IsCancellationRequested) { Log.Write($"channel switch failed: {ex.Message}"); }
        finally { _picker.SwitchFinished(); }
    }

    async Task ConnectRememberedAsync()
    {
        if (_connecting) return;
        _connecting = true;
        try
        {
            foreach (var remembered in Remembered)
            {
                if (_backend.Connected.FirstOrDefault(d => d.Kind == remembered.Kind && remembered.Matches(d.Name)) is { } here)
                {
                    Say(ConnectText.AlreadyHere(here.Name));
                    continue;
                }
                Say(ConnectText.Calling(remembered.Name));
                var result = await _connector.ConnectAsync(remembered, _cts.Token);
                Say(ConnectText.For(result, remembered.Name));
            }
        }
        catch (Exception ex) when (!_cts.IsCancellationRequested) { Log.Write($"connect failed: {ex.Message}"); }
        finally { _connecting = false; }
    }

    void OnPetHovered(int x, int y)
    {
        if (!_picker.IsOpen || _frame is null) return;
        int? channel = _frame.HitTest(x, y, out _, out int c) == HitTarget.Channel ? c : null;
        if (channel == _picker.Hovered) return;
        _picker.Hover(channel, _clock.Elapsed);
        _window.SetHandCursor(channel is int n && _picker.CanPick(n));
        if (channel is int hovered && _picker.Look(hovered) == BubbleLook.Empty) Say(SwitchText.Empty(hovered));
        Render();
    }

    void ClosePicker()
    {
        if (!_picker.IsOpen) return;
        _picker.Close(_clock.Elapsed);
        _window.SetHandCursor(false);
        Render();
    }

    void Say(string text)
    {
        _bubbleTimer.Stop();
        _window.ShowBubble(text);
        _bubbleTimer.Start();
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
        if (_picker.Tick(now) && !_picker.IsOpen) _window.SetHandCursor(false);
        var spec = _animator.FrameAt(now) with { Picker = _picker.DrawingAt(now) };
        _frame = FrameComposer.Compose(_layout, _sprites, _placements, spec, _settings.BatteryStyle);
        _bitmap.WritePixels(new Int32Rect(0, 0, _frame.Width, _frame.Height), _frame.Pixels, _frame.Width * 4, 0);
#if DIAG_LOG
        _renders++;
#endif

        // Moving bubbles get a faster redraw than the pet's own frame rate, so they glide rather than jump.
        var interval = _picker.NeedsTicks ? HostPicker.FrameInterval : PetAnimator.FrameInterval;
        if (_frameTimer.Interval != interval) _frameTimer.Interval = interval;
        if (_animator.NeedsTicks(now) || _picker.NeedsTicks) { if (!_frameTimer.IsEnabled) _frameTimer.Start(); }
        else _frameTimer.Stop();

        _idleTimer.Stop();
        var wake = _picker.ClosesAt is TimeSpan closes && closes < _animator.NextIdleAt ? closes : _animator.NextIdleAt;
        var untilIdle = wake - now;
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
