namespace BatteryBuddy.Pet.Ui;

/// <summary>
/// The pet behaves like the mouse pointer over full-screen content (a video, a game, a slideshow): it stays out of
/// the way while the mouse rests, and shows while the mouse moves. Pure: the caller passes the time.
/// </summary>
public sealed class FullScreenPeek
{
    /// <summary>How long the mouse rests before the pet hides, like a video player's pointer.</summary>
    public static readonly TimeSpan IdleHide = TimeSpan.FromSeconds(3);

    TimeSpan _lastMove = TimeSpan.MinValue;

    public bool IsFullScreen { get; private set; }

    /// <summary>When the pet hides unless the mouse moves; null while it stays shown.</summary>
    public TimeSpan? HidesAt => IsFullScreen ? _lastMove + IdleHide : null;

    public bool IsShown(TimeSpan now) => HidesAt is not TimeSpan hides || now < hides;

    /// <summary>Full-screen content covers the pet's monitor (or no longer does). Entering it counts as a move, as for the pointer.</summary>
    public void SetFullScreen(bool on, TimeSpan now)
    {
        if (on && !IsFullScreen) _lastMove = now;
        IsFullScreen = on;
    }

    public void MouseMoved(TimeSpan now) => _lastMove = now;
}

public static class FullScreenCheck
{
    /// <summary>Whether a window's bounds cover a whole monitor (a borderless window may spill past its edges).</summary>
    public static bool Covers(ScreenRect window, ScreenRect monitor) =>
        window.X <= monitor.X && window.Y <= monitor.Y
        && window.X + window.Width >= monitor.X + monitor.Width
        && window.Y + window.Height >= monitor.Y + monitor.Height;
}
