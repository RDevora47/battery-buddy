namespace BatteryBuddy.Backend;

/// <summary>
/// How much use a battery has left, from a week of <see cref="DrainSegment"/>s:
/// <list type="number">
/// <item>The week gives each 10 % band its own drain rate (batteries rarely drain linearly), pulled toward the
/// weekly average when the band has little data: R̂ = (dropped + k) / (hours + k / average).</item>
/// <item>The last half hour gives the current pace against those rates: f = (actual + c) / (expected + c), so a
/// single coarse step barely moves it, clamped to [0.5, 3].</item>
/// <item>The projection drains band by band at R̂ · (1 + (f − 1) · e^(−t/τ)): the current pace fades back to the
/// usual one, so a burst now weighs on the next hour, not on next week.</item>
/// </list>
/// </summary>
public static class TimeLeftEstimator
{
    public static readonly TimeSpan PaceWindow = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan PaceFade = TimeSpan.FromHours(1);
    const int Bands = 10;
    const double BandPrior = 5;        // k: % of evidence at the weekly average every band starts with
    const double PaceSmoothing = 2;    // c: %
    const double MinPace = 0.5, MaxPace = 3;
    const double MinHours = 2, MinDrop = 5;

    /// <param name="levelSince">When the device reached its current level while measured, or null if it isn't.</param>
    /// <returns>Null until the week holds enough data.</returns>
    public static TimeSpan? Estimate(IEnumerable<DrainSegment> segments, int level, DateTimeOffset? levelSince, DateTimeOffset now)
    {
        var week = segments.Where(s => s.End > now - DrainHistory.Kept && s.End <= now).ToList();
        var dropped = new double[Bands];
        var hours = new double[Bands];
        foreach (var segment in week)
            Spread(segment, 1, (band, percent, h) => { dropped[band] += percent; hours[band] += h; });
        if (hours.Sum() < MinHours || dropped.Sum() < MinDrop) return null;
        if (level <= 0) return TimeSpan.Zero;

        double average = dropped.Sum() / hours.Sum();
        var rates = Enumerable.Range(0, Bands).Select(b => (dropped[b] + BandPrior) / (hours[b] + BandPrior / average)).ToArray();
        return Project(rates, level, RecentPace(week, rates, level, levelSince, now));
    }

    public static double Pace(double actual, double expected) =>
        Math.Clamp((actual + PaceSmoothing) / (expected + PaceSmoothing), MinPace, MaxPace);

    static double RecentPace(List<DrainSegment> week, double[] rates, int level, DateTimeOffset? levelSince, DateTimeOffset now)
    {
        var start = now - PaceWindow;
        double actual = 0, expected = 0;
        foreach (var segment in week.Where(s => s.End > start))
        {
            double inWindow = segment.Start >= start ? 1 : (segment.End - start) / (segment.End - segment.Start);
            Spread(segment, inWindow, (band, percent, h) => { actual += percent; expected += h * rates[band]; });
        }
        // Time at the current level without a drop yet counts too: a still battery slows the pace.
        if (levelSince is DateTimeOffset since && since < now)
        {
            var from = since > start ? since : start;
            expected += (now - from).TotalHours * rates[Band(level)];
        }
        return Pace(actual, expected);
    }

    /// <summary>Each % dropped goes to its band with an equal share of the segment's time; <paramref name="share"/> scales both.</summary>
    static void Spread(DrainSegment segment, double share, Action<int, double, double> add)
    {
        int steps = segment.From - segment.To;
        if (steps <= 0) return;
        double hoursPerStep = (segment.End - segment.Start).TotalHours / steps;
        for (int percent = segment.From; percent > segment.To; percent--)
            add(Band(percent), share, hoursPerStep * share);
    }

    static TimeSpan Project(double[] rates, int level, double pace)
    {
        double tau = PaceFade.TotalMinutes, minutes = 0, left = level;
        // Minute by minute while the pace still differs from the usual...
        while (left > 0 && minutes < 6 * tau)
        {
            double perMinute = rates[Band(left)] / 60 * (1 + (pace - 1) * Math.Exp(-minutes / tau));
            if (perMinute >= left)
            {
                minutes += left / perMinute;
                left = 0;
                break;
            }
            left -= perMinute;
            minutes++;
        }
        // ...then band by band at the usual rates.
        while (left > 0)
        {
            int band = Band(left);
            double bottom = band * 10;
            minutes += (left - bottom) / rates[band] * 60;
            left = bottom;
        }
        return TimeSpan.FromMinutes(minutes);
    }

    /// <summary>The band the next % below <paramref name="level"/> drains in: 91–100 → 9, 1–10 → 0.</summary>
    static int Band(double level) => Math.Clamp(((int)Math.Ceiling(level) - 1) / 10, 0, Bands - 1);
}
