using BatteryBuddy.Devices;
using BatteryBuddy.Pet.Scene;

namespace BatteryBuddy.Pet.Animation;

public enum PickerPhase { Closed, Opening, Open, Closing, Leaving }

/// <summary>
/// The channel picker over a mouse: bubbles rise out of it, wait for a pick, then sink back (closed without
/// one) or fly into the chosen bubble (switching). Pure: the caller passes the time and calls Tick so moves
/// finish and the idle timeout fires.
/// </summary>
public sealed class HostPicker
{
    /// <summary>How long rising, sinking and flying take.</summary>
    public static readonly TimeSpan MoveTime = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(8);

    /// <summary>While bubbles move, the frame is redrawn this often (~30 fps) so the move is smooth; the pet itself stays at its own pace.</summary>
    public static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(33);

    readonly int _frameWidth;
    IReadOnlyList<HostChannel> _shown = Array.Empty<HostChannel>();
    int _current;
    PixelPoint _mouse;
    IReadOnlyList<PixelPoint> _targets = Array.Empty<PixelPoint>();
    TimeSpan _phaseStart, _lastActivity;
    int _chosen;

    public HostPicker(int frameWidth) => _frameWidth = frameWidth;

    public PickerPhase Phase { get; private set; }
    public bool IsOpen => Phase != PickerPhase.Closed;

    /// <summary>The mouse this picker is for, while open.</summary>
    public string? DeviceKey { get; private set; }

    public int? Hovered { get; private set; }

    public bool NeedsTicks => Phase is PickerPhase.Opening or PickerPhase.Closing or PickerPhase.Leaving;

    /// <summary>When the idle timeout closes it, while the bubbles are out.</summary>
    public TimeSpan? ClosesAt => Phase is PickerPhase.Opening or PickerPhase.Open ? _lastActivity + IdleTimeout : null;

    public void Open(string deviceKey, PixelPoint mouse, HostChannels channels, TimeSpan now)
    {
        DeviceKey = deviceKey;
        _mouse = mouse;
        _shown = channels.Channels.Take(ChannelArc.MaxChannels).ToList();
        _current = channels.Current;
        _targets = ChannelArc.Positions(mouse, _shown.Count, _frameWidth);
        Hovered = null;
        Enter(PickerPhase.Opening, now);
    }

    public BubbleLook? Look(int channel) => _shown.FirstOrDefault(c => c.Number == channel) switch
    {
        null => null,
        { Number: var n } when n == _current => BubbleLook.Current,
        { Paired: false } => BubbleLook.Empty,
        _ => BubbleLook.Pickable,
    };

    public bool CanPick(int channel) => Phase is PickerPhase.Opening or PickerPhase.Open && Look(channel) == BubbleLook.Pickable;

    public void Hover(int? channel, TimeSpan now)
    {
        Hovered = channel;
        _lastActivity = now;
    }

    public void Pick(int channel, TimeSpan now)
    {
        if (!CanPick(channel)) return;
        _chosen = channel;
        Enter(PickerPhase.Leaving, now);
    }

    public void Close(TimeSpan now)
    {
        if (Phase is PickerPhase.Opening or PickerPhase.Open) Enter(PickerPhase.Closing, now);
    }

    /// <summary>Gone at once, no animation (the mouse disconnected).</summary>
    public void Reset()
    {
        Phase = PickerPhase.Closed;
        DeviceKey = null;
        Hovered = null;
        _shown = Array.Empty<HostChannel>();
    }

    /// <summary>Finishes moves and fires the idle timeout. True if the phase changed.</summary>
    public bool Tick(TimeSpan now)
    {
        var before = Phase;
        if (Phase == PickerPhase.Opening && now - _phaseStart >= MoveTime) Phase = PickerPhase.Open;
        if (Phase == PickerPhase.Open && now - _lastActivity >= IdleTimeout) Enter(PickerPhase.Closing, now);
        if (Phase is PickerPhase.Closing or PickerPhase.Leaving && now - _phaseStart >= MoveTime) Reset();
        return Phase != before;
    }

    public PickerDrawing? DrawingAt(TimeSpan now)
    {
        if (Phase == PickerPhase.Closed) return null;
        double t = EaseOut(Math.Clamp((now - _phaseStart) / MoveTime, 0, 1));
        // Bubbles start (and end, sinking back) just over the mouse.
        var home = new PixelPoint(_mouse.X, _mouse.Y - 1);
        var chosen = Phase == PickerPhase.Leaving ? _targets[IndexOf(_chosen)] : default;
        var bubbles = new List<ChannelBubble>();
        for (int i = 0; i < _shown.Count; i++)
        {
            var target = _targets[i];
            var at = Phase switch
            {
                PickerPhase.Opening => Lerp(home, target, t),
                PickerPhase.Closing => Lerp(target, home, t),
                PickerPhase.Leaving => Lerp(target, chosen, t),
                _ => target,
            };
            var channel = _shown[i];
            bubbles.Add(new ChannelBubble(channel.Number, Look(channel.Number)!.Value, at, channel.Number == Hovered));
        }
        return new PickerDrawing(bubbles, _mouse, Trail: Phase == PickerPhase.Open);
    }

    void Enter(PickerPhase phase, TimeSpan now)
    {
        Phase = phase;
        _phaseStart = now;
        _lastActivity = now;
    }

    int IndexOf(int channel)
    {
        for (int i = 0; i < _shown.Count; i++)
            if (_shown[i].Number == channel) return i;
        return 0;
    }

    static double EaseOut(double t) => 1 - (1 - t) * (1 - t);

    static PixelPoint Lerp(PixelPoint from, PixelPoint to, double t) =>
        new((int)Math.Round(from.X + (to.X - from.X) * t), (int)Math.Round(from.Y + (to.Y - from.Y) * t));
}
