using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BatteryBuddy.Core.Animation;
using BatteryBuddy.Core.Scene;
using BatteryBuddy.Core.Ui;

namespace BatteryBuddy.App;

sealed class PetController : IDisposable
{
    readonly PetWindow _window;
    readonly SkinLayout _layout;
    readonly IReadOnlyDictionary<string, Sprite> _sprites;
    readonly PetAnimator _animator;
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly WriteableBitmap _bitmap;
    readonly DispatcherTimer _frameTimer;
    readonly DispatcherTimer _idleTimer;
    Settings _settings = Settings.Load();
    IReadOnlyList<DevicePlacement> _placements = Array.Empty<DevicePlacement>();
    ComposedFrame? _frame;

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

        _window.Moved += SavePosition;
    }

    public Sprite IconSprite => _sprites["body_idle"];

    public Task StartAsync()
    {
        PlaceWindow();
        _window.Show();
        Render();
        return Task.CompletedTask;
    }

    public Task RescanAsync() => Task.CompletedTask;

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
        _frameTimer.Stop();
        _idleTimer.Stop();
    }
}
