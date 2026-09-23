namespace BatteryBuddy.Backend;

/// <summary>What was learned about a device's charging and reporting pace; persisted per device.</summary>
public sealed record DeviceChargeStats(double? MinutesPerPercent, int? LastStep, double? ReportIntervalMinutes);

/// <summary>
/// Infers charging from rising battery levels (Windows doesn't report charging state).
/// After a rise, the device counts as charging until the next rise is overdue:
/// expected wait = learned minutes-per-1 % × last step (or, before two consecutive rises,
/// the learned report interval of periodic sources such as Windows, default 5 min), stretched by a
/// charge-aware tolerance.
/// </summary>
public sealed class ChargeTracker
{
    public const double Alpha = 0.3;
    public static readonly TimeSpan DefaultReportInterval = TimeSpan.FromMinutes(5);

    sealed class Session
    {
        public int Level;
        public DateTimeOffset ChangedAt;
        public DateTimeOffset? LastRiseAt;
        public DateTimeOffset? Until;
    }

    // Sessions are per device *and* source, so interleaved Windows/Buds readings never look like rises.
    readonly Dictionary<(string Key, string Source), Session> _sessions = new();
    readonly Dictionary<string, DeviceChargeStats> _stats;

    public ChargeTracker(IReadOnlyDictionary<string, DeviceChargeStats>? learned = null)
    {
        _stats = learned?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new();
    }

    public IReadOnlyDictionary<string, DeviceChargeStats> Stats => _stats;

    /// <summary>Raised when learned stats change, so they can be saved.</summary>
    public event Action? StatsChanged;

    /// <summary>2× at 90–100 %, 0.1 less per 10 % band below (1.1× at 0–9 %): charging slows as it fills.</summary>
    public static double Tolerance(int level) =>
        level >= 90 ? 2.0 : (11 + Math.Max(0, level) / 10) / 10.0;

    /// <param name="periodic">The source reports levels on its own schedule, so the gap between changes teaches its report interval.</param>
    public void Observe(string key, string source, int level, DateTimeOffset at, bool periodic = true)
    {
        if (!_sessions.TryGetValue((key, source), out var session))
        {
            _sessions[(key, source)] = new Session { Level = level, ChangedAt = at };
            return;
        }
        if (level == session.Level) return;

        var before = _stats.GetValueOrDefault(key) ?? new DeviceChargeStats(null, null, null);
        var stats = before;
        if (periodic)
            stats = stats with { ReportIntervalMinutes = Ema(stats.ReportIntervalMinutes, (at - session.ChangedAt).TotalMinutes) };

        if (level > session.Level)
        {
            int step = level - session.Level;
            if (session.LastRiseAt is DateTimeOffset previousRise)
                stats = stats with
                {
                    MinutesPerPercent = Ema(stats.MinutesPerPercent, (at - previousRise).TotalMinutes / step),
                    LastStep = step,
                };
            double expectedMinutes = stats.MinutesPerPercent is double perPercent
                ? perPercent * step
                : before.ReportIntervalMinutes ?? DefaultReportInterval.TotalMinutes;
            session.LastRiseAt = at;
            session.Until = at + TimeSpan.FromMinutes(expectedMinutes * Tolerance(level));
        }
        else
        {
            session.LastRiseAt = null;
            session.Until = null;
        }

        session.Level = level;
        session.ChangedAt = at;
        if (stats != before)
        {
            _stats[key] = stats;
            StatsChanged?.Invoke();
        }
    }

    public bool IsCharging(string key, DateTimeOffset now) => ChargingUntil(key) is DateTimeOffset until && now < until;

    public DateTimeOffset? ChargingUntil(string key) =>
        _sessions.Where(s => s.Key.Key == key && s.Value.Until is not null)
                 .Select(s => s.Value.Until)
                 .Max();

    /// <summary>Device disconnected: drop the in-progress session, keep what was learned.</summary>
    public void Forget(string key)
    {
        foreach (var id in _sessions.Keys.Where(k => k.Key == key).ToList())
            _sessions.Remove(id);
    }

    static double Ema(double? previous, double sample) =>
        previous is double p ? Alpha * sample + (1 - Alpha) * p : sample;
}
