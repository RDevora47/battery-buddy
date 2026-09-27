using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using BatteryBuddy.Pet.Ui;
using WinForms = System.Windows.Forms;

namespace BatteryBuddy.App;

public partial class PetWindow : Window
{
    /// <summary>Screen DIPs per art pixel.</summary>
    public const int Scale = 4;
    const double DragThreshold = 4;
    // Transparent room around the pet so the bubble can sit above or below it and shift sideways.
    const double MinSideRoom = 44;
    const double BubbleRoom = 140;
    const double BubbleShadow = 4;
    const double BubbleMaxWidth = 212 + BubbleShadow; // Bubble's MaxWidth in the XAML

    const int WM_DISPLAYCHANGE = 0x007E;
    const int WM_SETTINGCHANGE = 0x001A;
    const int SPI_SETWORKAREA = 0x002F;

    Point _pressedAt;
    bool _pressed;
    int _resolution = 1;
    double _sideRoom = MinSideRoom;
    // While dragging: the cursor and the window's top-left when the drag started, in screen pixels.
    (System.Drawing.Point Cursor, int Left, int Top)? _drag;

    public PetWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            NativeMethods.MakeToolWindowNoActivate(source.Handle);
            source.AddHook(WndProc);
        };
        LocationChanged += (_, _) => PlaceBubble();
    }

    /// <summary>
    /// Windows sometimes drops a topmost window below ordinary ones (e.g. around full-screen apps) while it
    /// keeps reporting itself as topmost, so WPF never re-applies it and the pet hides behind other windows.
    /// Cheap enough to call on every idle animation.
    /// </summary>
    public void KeepOnTop()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !IsVisible || !NativeMethods.LostTopmost(hwnd)) return;
        NativeMethods.RestoreTopmost(hwnd);
        Log.Write("pet window had dropped below other windows; put it back on top");
    }

    /// <summary>Click on the pet in frame pixel coordinates (art pixels x resolution, not screen pixels).</summary>
    public event Action<int, int>? PetClicked;

    /// <summary>Right-click on the pet, in frame pixel coordinates.</summary>
    public event Action<int, int>? PetRightClicked;

    /// <summary>The pointer moved over the pet (frame pixels), or left it (-1, -1).</summary>
    public event Action<int, int>? PetHovered;

    public event Action? Moved;
    public event Action? MenuRequested;

    /// <summary>Where the pet image sits inside the window, in DIPs.</summary>
    public ScreenRect PetInWindow => new(_sideRoom, BubbleRoom, PetImage.Width, PetImage.Height);

    /// <summary>resolution: bitmap pixels per art pixel.</summary>
    public void SetBitmap(WriteableBitmap bitmap, int resolution)
    {
        _resolution = resolution;
        PetImage.Source = bitmap;
        PetImage.Width = bitmap.PixelWidth * Scale / resolution;
        PetImage.Height = bitmap.PixelHeight * Scale / resolution;
        // With half the pet off the left or right edge, the bubble is shifted onto the screen, which puts it
        // up to its full width past the pet's inner half; the window must be wide enough to hold it there.
        _sideRoom = Math.Max(MinSideRoom, BubbleMaxWidth - PetImage.Width / 2);
        Canvas.SetLeft(PetImage, _sideRoom);
        Canvas.SetTop(PetImage, BubbleRoom);
        Width = PetImage.Width + 2 * _sideRoom;
        Height = PetImage.Height + 2 * BubbleRoom;
    }

    /// <summary>Monitor work areas (screen minus taskbar) in DIPs.</summary>
    public IReadOnlyList<ScreenRect> WorkAreas() => WinForms.Screen.AllScreens.Select(s => ToDip(s.WorkingArea)).ToList();

    public ScreenRect PrimaryWorkArea() => ToDip(WinForms.Screen.PrimaryScreen!.WorkingArea);

    ScreenRect ToDip(System.Drawing.Rectangle r)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return new(r.X / dpi.DpiScaleX, r.Y / dpi.DpiScaleY, r.Width / dpi.DpiScaleX, r.Height / dpi.DpiScaleY);
    }

    ScreenRect PetOnScreen => PetInWindow.Offset(Left, Top);

    ScreenRect PetWorkArea => ScreenClamp.AreaFor(PetOnScreen.CenterX, PetOnScreen.CenterY, WorkAreas());

    /// <summary>Pulls the pet back to at least half on its monitor's work area (after a display or taskbar change).</summary>
    public void KeepOnScreen()
    {
        var (dx, dy) = ScreenClamp.Into(PetOnScreen, PetWorkArea, WindowPlacement.PetOverhang);
        if (dx == 0 && dy == 0) return;
        Left += dx;
        Top += dy;
        Moved?.Invoke();
    }

    public void ShowBubble(string text)
    {
        BubbleLabel.Text = text;
        Bubble.BeginAnimation(OpacityProperty, null);
        Bubble.Opacity = 1;
        Bubble.Visibility = Visibility.Visible;
        PlaceBubble();
    }

    public void HideBubble()
    {
        if (Bubble.Visibility != Visibility.Visible) return;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
        fade.Completed += (_, _) => Bubble.Visibility = Visibility.Collapsed;
        Bubble.BeginAnimation(OpacityProperty, fade);
    }

    void PlaceBubble()
    {
        if (Bubble.Visibility != Visibility.Visible) return;
        Bubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double width = Bubble.DesiredSize.Width + BubbleShadow, height = Bubble.DesiredSize.Height + BubbleShadow;
        var (x, y) = BubblePlacement.Resolve(PetOnScreen, width, height, PetWorkArea);
        Canvas.SetLeft(Bubble, Math.Clamp(x - Left, 0, Math.Max(0, Width - width)));
        Canvas.SetTop(Bubble, Math.Clamp(y - Top, 0, Math.Max(0, Height - height)));
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_DISPLAYCHANGE || (msg == WM_SETTINGCHANGE && wParam == SPI_SETWORKAREA))
            Dispatcher.BeginInvoke(KeepOnScreen); // after WPF and WinForms have seen the new layout
        return IntPtr.Zero;
    }

    // The drag is done by hand rather than with DragMove: Windows' move loop won't let a window's top edge
    // go above the top of the screen, and the bubble room above the pet kept it a whole bubble away from it.
    void BeginDrag()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.GetWindowRect(hwnd, out var rect);
        _drag = (WinForms.Cursor.Position, rect.Left, rect.Top);
    }

    void DragTo(System.Drawing.Point cursor)
    {
        if (_drag is not var (from, left, top)) return;
        int x = left + cursor.X - from.X, y = top + cursor.Y - from.Y;

        // Clamp in screen pixels, against the monitor under the cursor so the pet can still cross monitors.
        var dpi = VisualTreeHelper.GetDpi(this);
        var pet = PetInWindow;
        var petPx = new ScreenRect(x + pet.X * dpi.DpiScaleX, y + pet.Y * dpi.DpiScaleY,
            pet.Width * dpi.DpiScaleX, pet.Height * dpi.DpiScaleY);
        var areasPx = WinForms.Screen.AllScreens.Select(s => ToRect(s.WorkingArea)).ToList();
        var (dx, dy) = ScreenClamp.Into(petPx, ScreenClamp.AreaFor(cursor.X, cursor.Y, areasPx), WindowPlacement.PetOverhang);
        NativeMethods.MoveWindow(new WindowInteropHelper(this).Handle, x + PixelsTowardArea(dx), y + PixelsTowardArea(dy));
    }

    void EndDrag()
    {
        if (_drag is null) return;
        _drag = null;
        Moved?.Invoke();
    }

    // Rounding toward zero could leave the pet a pixel further off screen than allowed.
    static int PixelsTowardArea(double d) => (int)(d > 0 ? Math.Ceiling(d) : Math.Floor(d));

    static ScreenRect ToRect(System.Drawing.Rectangle r) => new(r.X, r.Y, r.Width, r.Height);

    void PetImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressedAt = e.GetPosition(this);
        _pressed = true;
        PetImage.CaptureMouse();
    }

    void PetImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (_drag is not null)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragTo(WinForms.Cursor.Position);
            else PetImage.ReleaseMouseCapture(); // missed the button-up; LostMouseCapture ends the drag
            return;
        }
        if (!_pressed || e.LeftButton != MouseButtonState.Pressed)
        {
            var (hx, hy) = FramePoint(e);
            PetHovered?.Invoke(hx, hy);
            return;
        }
        var delta = e.GetPosition(this) - _pressedAt;
        if (Math.Abs(delta.X) <= DragThreshold && Math.Abs(delta.Y) <= DragThreshold) return;

        _pressed = false;
        BeginDrag(); // keeps the mouse captured until the button comes up
    }

    void PetImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        PetImage.ReleaseMouseCapture();
        if (!_pressed) return;
        _pressed = false;
        var (x, y) = FramePoint(e);
        PetClicked?.Invoke(x, y);
    }

    void PetImage_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var (x, y) = FramePoint(e);
        PetRightClicked?.Invoke(x, y);
    }

    void PetImage_MouseLeave(object sender, MouseEventArgs e) => PetHovered?.Invoke(-1, -1);

    void PetImage_LostMouseCapture(object sender, MouseEventArgs e) => EndDrag();

    /// <summary>Opens the tray menu (the controller decides when a right-click means that).</summary>
    public void RequestMenu() => MenuRequested?.Invoke();

    public void SetHandCursor(bool hand) => PetImage.Cursor = hand ? Cursors.Hand : null;

    (int X, int Y) FramePoint(MouseEventArgs e)
    {
        var p = e.GetPosition(PetImage);
        return ((int)(p.X * _resolution / Scale), (int)(p.Y * _resolution / Scale));
    }
}
