using BatteryBuddy.Pet.Scene;

namespace BatteryBuddy.Pet.Animation;

public sealed class PetAnimator
{
    public const int Fps = 6;
    public static readonly TimeSpan FrameInterval = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / Fps);

    static readonly int[] HappyBob = { 0, 1, 1, 0, 1, 0 };
    // A happy pet with a tail swings it through the same burst, coming to rest on the last frame.
    static readonly string[] HappyWag = { "tail_wag1", "tail_wag2", "tail_wag1", "tail_wag2", "tail_wag1", "tail" };
    // With a critical device the idle burst shakes it instead (frames at Fps).
    static readonly int[] Shake = { -1, 1, -1, 1, -1, 1 };
    static readonly TimeSpan BurstLength = TimeSpan.FromTicks(FrameInterval.Ticks * HappyBob.Length);
    static readonly TimeSpan MinSniff = TimeSpan.FromSeconds(1);
    static readonly TimeSpan EffectLength = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 2);
    static readonly TimeSpan MouseHold = TimeSpan.FromSeconds(1);
    const int BlinkBurstFrame = 2;

    readonly Func<double> _random;
    readonly PixelPoint _zzz;
    readonly PixelPoint _sweat;
    readonly PixelPoint _saiyan;
    readonly PixelPoint _strawHat;
    readonly List<(Effect Kind, int X, int Y, TimeSpan Start)> _effects = new();
    TimeSpan? _sniffStart;
    TimeSpan? _sniffEndRequested;
    TimeSpan? _burstStart;
    TimeSpan _leftPawUp;    // a paw is pressed until this time
    TimeSpan _rightPawUp;
    TimeSpan _clickUp;      // the right paw is clicking (not typing) until this time
    TimeSpan _offMouse;     // the right paw rests on the mouse until this time
    Paw _nextTap = Paw.Left;

    enum Effect { Ploof, Whoosh }

    public PetAnimator(Func<double> random, TimeSpan now, PixelPoint zzz, PixelPoint sweat, PixelPoint saiyan = default, PixelPoint strawHat = default)
    {
        _strawHat = strawHat;
        _random = random;
        _zzz = zzz;
        _sweat = sweat;
        _saiyan = saiyan;
        ScheduleIdle(now);
    }

    public Mood Mood { get; set; }
    /// <summary>A device is at ≤ 10 %: the idle burst shakes it instead of bobbing, blinking or drifting the Zzz.</summary>
    public bool HasCritical { get; set; }

    /// <summary>Lowest connected battery; drives gill droop and color fade.</summary>
    public int? LowestBattery { get; set; }

    /// <summary>Every connected device is nearly full (see <see cref="MoodCalculator.SuperSaiyan"/>): the pet wears its <see cref="Hat"/>.</summary>
    public bool SuperSaiyan { get; set; }

    public FullChargeHat Hat { get; set; } = FullChargeHat.SaiyanHair;

    public TimeSpan NextIdleAt { get; private set; }

    public void BeginSniff(TimeSpan now)
    {
        _sniffStart = now;
        _sniffEndRequested = null;
    }

    public void EndSniff(TimeSpan now) => _sniffEndRequested = now;

    /// <summary>A device disconnected at (x, y): smoke puff and "ploof!".</summary>
    public void AddPloof(int x, int y, TimeSpan now) => _effects.Add((Effect.Ploof, x, y, now));

    /// <summary>A device connected at (x, y): speed lines streak in and end in a sparkle.</summary>
    public void AddWhoosh(int x, int y, TimeSpan now) => _effects.Add((Effect.Whoosh, x, y, now));

    /// <summary>A key went down: the paws take turns tapping, each press lasting one frame.</summary>
    public void KeyTap(TimeSpan now)
    {
        _offMouse = now;
        if (_nextTap == Paw.Left) (_leftPawUp, _rightPawUp) = (now + FrameInterval, now);
        else (_rightPawUp, _leftPawUp) = (now + FrameInterval, now);
        _nextTap = _nextTap == Paw.Left ? Paw.Right : Paw.Left;
    }

    /// <summary>A mouse button went down: the right paw reaches for the mouse and clicks, staying there a second.</summary>
    public void Click(TimeSpan now)
    {
        _rightPawUp = _clickUp = now + FrameInterval;
        _offMouse = now + MouseHold;
    }

    /// <summary>
    /// The mouse wheel turned: the right paw goes to the mouse and flicks, at most every other frame,
    /// so a long scroll wiggles the paw instead of holding it down.
    /// </summary>
    public void Scroll(TimeSpan now)
    {
        _offMouse = now + MouseHold;
        if (now < _clickUp + FrameInterval) return;
        _rightPawUp = _clickUp = now + FrameInterval;
    }

    public bool NeedsTicks(TimeSpan now) =>
        now < _leftPawUp || now < _rightPawUp || now < _offMouse || IsSniffing(now) || InBurst(now) || _effects.Any(e => now - e.Start < EffectLength);

    public FrameSpec FrameAt(TimeSpan now)
    {
        _effects.RemoveAll(e => now - e.Start >= EffectLength);
        // The idle burst only plays when nothing else is animating: it waits for the next slot and stops if interrupted.
        bool busy = OtherAnimationRunning(now);
        if (busy) _burstStart = null;
        if (now >= NextIdleAt)
        {
            if (!busy) _burstStart = now;
            ScheduleIdle(now);
        }

        var overlays = new List<Overlay>();
        string body;
        string tail = "tail";
        int dy = 0;
        int criticalDx = 0;

        if (IsSniffing(now))
        {
            body = FrameIndex(now, _sniffStart!.Value) % 4 < 2 ? "body_sniff_l" : "body_sniff_r";
        }
        else
        {
            _sniffStart = null;
            int? burst = InBurst(now) ? FrameIndex(now, _burstStart!.Value) : null;
            if (HasCritical && burst is int s)
            {
                criticalDx = Shake[s];
                burst = null;
            }
            switch (Mood)
            {
                case Mood.Happy:
                    body = burst == BlinkBurstFrame ? "body_blink" : "body_idle";
                    dy = burst is int f ? HappyBob[f] : 0;
                    if (burst is int w) tail = HappyWag[w];
                    break;
                case Mood.Sleepy:
                    body = "body_sleepy";
                    overlays.Add(new Overlay("zzz", _zzz.X, _zzz.Y - (burst ?? 0) / 2));
                    break;
                default:
                    body = "body_worried";
                    dy = burst is int g ? g % 2 : 0;
                    overlays.Add(new Overlay("sweat", _sweat.X, _sweat.Y + dy));
                    break;
            }
        }

        if (SuperSaiyan && Hat != FullChargeHat.None)
        {
            var (sprite, at) = Hat == FullChargeHat.StrawHat ? ("strawhat", _strawHat) : ("saiyan", _saiyan);
            overlays.Insert(0, new Overlay(sprite, at.X, at.Y + dy));
        }

        foreach (var (kind, x, y, start) in _effects)
        {
            int frame = Math.Min(2, FrameIndex(now, start));
            if (kind == Effect.Ploof)
            {
                overlays.Add(new Overlay($"smoke{frame + 1}", x - 2, y - 2));
                overlays.Add(new Overlay("ploof_text", x - 8, y - 8));
            }
            else
            {
                overlays.Add(new Overlay($"whoosh{frame + 1}", x - 3, y - 1));
            }
        }

        return new FrameSpec(body, dy, overlays, criticalDx,
            MoodCalculator.GillsFor(LowestBattery), MoodCalculator.FadeFor(LowestBattery),
            now < _leftPawUp ? 1 : 0, now < _rightPawUp ? 1 : 0, now < _clickUp ? 1 : 0, now < _offMouse, tail);
    }

    bool IsSniffing(TimeSpan now) =>
        _sniffStart is TimeSpan start &&
        !(_sniffEndRequested is TimeSpan end && now >= Max(end, start + MinSniff));

    bool OtherAnimationRunning(TimeSpan now) =>
        now < _leftPawUp || now < _rightPawUp || now < _offMouse || IsSniffing(now) || _effects.Count > 0;

    bool InBurst(TimeSpan now) => _burstStart is TimeSpan start && now - start < BurstLength;

    void ScheduleIdle(TimeSpan now) => NextIdleAt = now + TimeSpan.FromSeconds(3 + 5 * _random());

    static int FrameIndex(TimeSpan now, TimeSpan start) =>
        (int)((now - start).Ticks * Fps / TimeSpan.TicksPerSecond);

    static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
