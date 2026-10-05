using BatteryBuddy.Backend;

namespace BatteryBuddy.Tests;

public class TimeLeftEstimatorTests
{
    static readonly DateTimeOffset Now = TestReadings.T0;

    /// <summary>1 % segments from <paramref name="from"/> down to <paramref name="to"/> at a steady rate, the last ending at <paramref name="end"/>.</summary>
    static IEnumerable<DrainSegment> Drain(int from, int to, double percentPerHour, DateTimeOffset end)
    {
        var step = TimeSpan.FromHours(1 / percentPerHour);
        var start = end - step * (from - to);
        for (int level = from; level > to; level--, start += step)
            yield return new DrainSegment(start, start + step, level, level - 1);
    }

    static double Hours(TimeSpan? estimate) => Assert.NotNull(estimate).TotalHours;

    [Fact]
    public void Too_little_history_gives_no_estimate()
    {
        // 4 % dropped: under the 5 % minimum.
        Assert.Null(TimeLeftEstimator.Estimate(Drain(80, 76, 1, Now.AddHours(-1)), 50, null, Now));
        // 10 % dropped in an hour: under the 2 h minimum.
        Assert.Null(TimeLeftEstimator.Estimate(Drain(80, 70, 10, Now.AddHours(-1)), 50, null, Now));
    }

    [Fact]
    public void History_older_than_a_week_is_ignored() =>
        Assert.Null(TimeLeftEstimator.Estimate(Drain(100, 0, 10, Now.AddDays(-8)), 50, null, Now));

    [Fact]
    public void A_steady_drain_divides_evenly() =>
        Assert.Equal(5, Hours(TimeLeftEstimator.Estimate(Drain(100, 0, 10, Now.AddHours(-1)), 50, null, Now)), 2);

    [Fact]
    public void Each_band_drains_at_its_own_rate()
    {
        // Ten 9 h cycles in which the last 20 % drain twice as fast.
        var history = Enumerable.Range(0, 10).SelectMany(cycle =>
        {
            var end = Now.AddHours(-1 - 9 * cycle);
            return Drain(100, 20, 10, end.AddHours(-1)).Concat(Drain(20, 0, 20, end));
        });
        // 30 → 20 at 10 %/h, then 20 → 0 at 20 %/h (a little less: the bands lean on the 11 %/h average).
        Assert.Equal(2, Hours(TimeLeftEstimator.Estimate(history, 30, null, Now)), 1);
    }

    [Fact]
    public void Sparse_bands_lean_on_the_weekly_average()
    {
        // 90s: 10 % in 1 h, 80s: 10 % in 3 h, nothing below. Weekly average 20 % / 4 h = 5 %/h.
        var history = Drain(100, 90, 10, Now.AddHours(-4)).Concat(Drain(90, 80, 10 / 3.0, Now.AddHours(-1)));
        // 90s: (10 + 5) / (1 + 1) = 7.5 %/h; 80s: 15 / (3 + 1) = 3.75 %/h; empty bands 5 / 1 = 5 %/h.
        double expected = 10 / 7.5 + 10 / 3.75 + 80 / 5.0;
        Assert.Equal(expected, Hours(TimeLeftEstimator.Estimate(history, 100, null, Now)), 2);
    }

    [Fact]
    public void A_still_level_slows_the_pace_until_it_fades()
    {
        // 30 min at 50 % without a drop where 5 % was expected: pace (0 + 2) / (5 + 2), clamped to 0.5.
        var history = Drain(100, 0, 10, Now.AddDays(-1));
        // 10 %/h · (T − 0.5 · (1 − e^−T)) = 50 % → T ≈ 5.497 h.
        Assert.Equal(5.497, Hours(TimeLeftEstimator.Estimate(history, 50, Now.AddMinutes(-30), Now)), 1);
    }

    [Fact]
    public void A_fast_recent_drain_shortens_the_estimate_but_fades()
    {
        // A week at 10 %/h, then the last 30 min dropped 10 %.
        var history = Drain(100, 60, 10, Now.AddHours(-1)).Concat(Drain(60, 50, 20, Now));
        double hours = Hours(TimeLeftEstimator.Estimate(history, 50, Now, Now));
        double usual = Hours(TimeLeftEstimator.Estimate(history.Where(s => s.End <= Now.AddHours(-1)), 50, null, Now));
        Assert.True(hours < usual - 0.4, $"{hours} h should be well under the usual {usual} h");
        Assert.True(hours > usual / 3, $"{hours} h: the fast pace should fade, not apply to the whole battery");
    }

    [Theory]
    [InlineData(5, 5, 1)]
    [InlineData(1, 0, 1.5)]      // one coarse step with nothing expected barely moves it
    [InlineData(30, 5, 3)]       // (30 + 2) / (5 + 2) ≈ 4.6, capped
    [InlineData(0, 5, 0.5)]      // 2 / 7 ≈ 0.29, floored
    public void Pace_compares_the_last_half_hour_with_the_usual(double actual, double expected, double pace) =>
        Assert.Equal(pace, TimeLeftEstimator.Pace(actual, expected), 3);

    [Fact]
    public void An_empty_battery_has_no_time_left() =>
        Assert.Equal(TimeSpan.Zero, TimeLeftEstimator.Estimate(Drain(100, 0, 10, Now.AddHours(-1)), 0, null, Now));
}
