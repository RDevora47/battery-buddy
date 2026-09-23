using BatteryBuddy.Core.Scene;

namespace BatteryBuddy.Core.Animation;

public sealed class PetAnimator
{
    public const int Fps = 6;
    public static readonly TimeSpan FrameInterval = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / Fps);

    static readonly int[] HappyBob = { 0, 1, 1, 0, 1, 0 };
    static readonly TimeSpan BurstLength = TimeSpan.FromTicks(FrameInterval.Ticks * HappyBob.Length);
    static readonly TimeSpan MinSniff = TimeSpan.FromSeconds(1);
    static readonly TimeSpan PloofLength = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 2);
    const int BlinkBurstFrame = 2;

    readonly Func<double> _random;
    readonly PixelPoint _zzz;
    readonly PixelPoint _sweat;
    readonly List<(int X, int Y, TimeSpan Start)> _ploofs = new();
    TimeSpan? _sniffStart;
    TimeSpan? _sniffEndRequested;
    TimeSpan? _burstStart;

    public PetAnimator(Func<double> random, TimeSpan now, PixelPoint zzz, PixelPoint sweat)
    {
        _random = random;
        _zzz = zzz;
        _sweat = sweat;
        ScheduleIdle(now);
    }

    public Mood Mood { get; set; }
    public bool HasCritical { get; set; }
    public TimeSpan NextIdleAt { get; private set; }

    public void BeginSniff(TimeSpan now)
    {
        _sniffStart = now;
        _sniffEndRequested = null;
    }

    public void EndSniff(TimeSpan now) => _sniffEndRequested = now;

    public void AddPloof(int x, int y, TimeSpan now) => _ploofs.Add((x, y, now));

    public bool NeedsTicks(TimeSpan now) =>
        HasCritical || IsSniffing(now) || InBurst(now) || _ploofs.Any(p => now - p.Start < PloofLength);

    public FrameSpec FrameAt(TimeSpan now)
    {
        if (now >= NextIdleAt)
        {
            _burstStart = now;
            ScheduleIdle(now);
        }
        _ploofs.RemoveAll(p => now - p.Start >= PloofLength);

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

        foreach (var (x, y, start) in _ploofs)
        {
            int frame = Math.Min(2, FrameIndex(now, start));
            overlays.Add(new Overlay($"smoke{frame + 1}", x - 2, y - 2));
            overlays.Add(new Overlay("ploof_text", x - 8, y - 8));
        }

        bool criticalVisible = now.Ticks / (TimeSpan.TicksPerSecond / 2) % 2 == 0;
        return new FrameSpec(body, dy, overlays, criticalVisible);
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
