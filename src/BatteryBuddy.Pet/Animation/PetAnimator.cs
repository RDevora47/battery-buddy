using BatteryBuddy.Pet.Scene;

namespace BatteryBuddy.Pet.Animation;

public sealed class PetAnimator
{
    public const int Fps = 6;
    public static readonly TimeSpan FrameInterval = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / Fps);

    static readonly int[] HappyBob = { 0, 1, 1, 0, 1, 0 };
    // Critical devices get shaken for a second, then rest for a second (frames at Fps).
    static readonly int[] Shake = { -1, 1, -1, 1, -1, 1, 0, 0, 0, 0, 0, 0 };
    static readonly TimeSpan BurstLength = TimeSpan.FromTicks(FrameInterval.Ticks * HappyBob.Length);
    static readonly TimeSpan MinSniff = TimeSpan.FromSeconds(1);
    static readonly TimeSpan EffectLength = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 2);
    static readonly TimeSpan MouseHold = TimeSpan.FromSeconds(1);
    const int BlinkBurstFrame = 2;

    readonly Func<double> _random;
    readonly PixelPoint _zzz;
    readonly PixelPoint _sweat;
    readonly PixelPoint _saiyan;
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

    public PetAnimator(Func<double> random, TimeSpan now, PixelPoint zzz, PixelPoint sweat, PixelPoint saiyan = default)
    {
        _random = random;
        _zzz = zzz;
        _sweat = sweat;
        _saiyan = saiyan;
        ScheduleIdle(now);
    }

    public Mood Mood { get; set; }
    public bool HasCritical { get; set; }

    /// <summary>Lowest connected battery; drives gill droop and color fade.</summary>
    public int? LowestBattery { get; set; }

    /// <summary>Every connected device is nearly full (see <see cref="MoodCalculator.SuperSaiyan"/>): Super Saiyan hair.</summary>
    public bool SuperSaiyan { get; set; }

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

    public bool NeedsTicks(TimeSpan now) =>
        now < _leftPawUp || now < _rightPawUp || now < _offMouse || HasCritical || IsSniffing(now) || InBurst(now) || _effects.Any(e => now - e.Start < EffectLength);

    public FrameSpec FrameAt(TimeSpan now)
    {
        if (now >= NextIdleAt)
        {
            _burstStart = now;
            ScheduleIdle(now);
        }
        _effects.RemoveAll(e => now - e.Start >= EffectLength);

        var overlays = new List<Overlay>();
        string body;
        int dy = 0;

        if (IsSniffing(now))
        {
            body = FrameIndex(now, _sniffStart!.Value) % 4 < 2 ? "body_sniff_l" : "body_sniff_r";
        }
        else
        {
            _sniffStart = null;
            int? burst = InBurst(now) ? FrameIndex(now, _burstStart!.Value) : null;
            switch (Mood)
            {
                case Mood.Happy:
                    body = burst == BlinkBurstFrame ? "body_blink" : "body_idle";
                    dy = burst is int f ? HappyBob[f] : 0;
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

        if (SuperSaiyan) overlays.Insert(0, new Overlay("saiyan", _saiyan.X, _saiyan.Y + dy));

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

        long frameNo = now.Ticks * Fps / TimeSpan.TicksPerSecond;
        return new FrameSpec(body, dy, overlays, Shake[frameNo % Shake.Length],
            MoodCalculator.GillsFor(LowestBattery), MoodCalculator.FadeFor(LowestBattery),
            now < _leftPawUp ? 1 : 0, now < _rightPawUp ? 1 : 0, now < _clickUp ? 1 : 0, now < _offMouse);
    }

    bool IsSniffing(TimeSpan now) =>
        _sniffStart is TimeSpan start &&
        !(_sniffEndRequested is TimeSpan end && now >= Max(end, start + MinSniff));

    bool InBurst(TimeSpan now) => _burstStart is TimeSpan start && now - start < BurstLength;

    void ScheduleIdle(TimeSpan now) => NextIdleAt = now + TimeSpan.FromSeconds(3 + 5 * _random());

    static int FrameIndex(TimeSpan now, TimeSpan start) =>
        (int)((now - start).Ticks * Fps / TimeSpan.TicksPerSecond);

    static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
