using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace BatteryBuddy.App;

public partial class PetWindow : Window
{
    /// <summary>Screen DIPs per art pixel.</summary>
    public const int Scale = 4;
    const double DragThreshold = 4;

    Point _pressedAt;
    bool _pressed;
    int _resolution = 1;

    public PetWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => NativeMethods.MakeToolWindowNoActivate(new WindowInteropHelper(this).Handle);
    }

    /// <summary>Click on the pet in frame pixel coordinates (art pixels x resolution, not screen pixels).</summary>
    public event Action<int, int>? PetClicked;
    public event Action? Moved;
    public event Action? MenuRequested;

    /// <summary>resolution: bitmap pixels per art pixel.</summary>
    public void SetBitmap(WriteableBitmap bitmap, int resolution)
    {
        _resolution = resolution;
        PetImage.Source = bitmap;
        PetImage.Width = bitmap.PixelWidth * Scale / resolution;
        PetImage.Height = bitmap.PixelHeight * Scale / resolution;
    }

    public void ShowBubble(string text)
    {
        BubbleLabel.Text = text;
        Bubble.BeginAnimation(OpacityProperty, null);
        Bubble.Opacity = 1;
        Bubble.Visibility = Visibility.Visible;
    }

    public void HideBubble()
    {
        if (Bubble.Visibility != Visibility.Visible) return;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
        fade.Completed += (_, _) => Bubble.Visibility = Visibility.Collapsed;
        Bubble.BeginAnimation(OpacityProperty, fade);
    }

    void PetImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressedAt = e.GetPosition(this);
        _pressed = true;
        PetImage.CaptureMouse();
    }

    void PetImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || e.LeftButton != MouseButtonState.Pressed) return;
        var delta = e.GetPosition(this) - _pressedAt;
        if (Math.Abs(delta.X) <= DragThreshold && Math.Abs(delta.Y) <= DragThreshold) return;

        _pressed = false;
        PetImage.ReleaseMouseCapture();
        DragMove();
        Moved?.Invoke();
    }

    void PetImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        PetImage.ReleaseMouseCapture();
        if (!_pressed) return;
        _pressed = false;
        var p = e.GetPosition(PetImage);
        PetClicked?.Invoke((int)(p.X * _resolution / Scale), (int)(p.Y * _resolution / Scale));
    }

    void PetImage_MouseRightButtonUp(object sender, MouseButtonEventArgs e) => MenuRequested?.Invoke();
}
