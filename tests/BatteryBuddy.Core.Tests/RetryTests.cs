using BatteryBuddy.Core.Retry;

namespace BatteryBuddy.Core.Tests;

public class RetryTests
{
    [Fact]
    public void Backoff_doubles_up_to_max()
    {
        var backoff = new Backoff(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
        var delays = Enumerable.Range(0, 7).Select(_ => backoff.NextDelay().TotalSeconds);
        Assert.Equal(new double[] { 10, 20, 40, 80, 160, 300, 300 }, delays);
    }

    [Fact]
    public void Backoff_reset_starts_over()
    {
        var backoff = new Backoff(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
        backoff.NextDelay();
        backoff.NextDelay();
        backoff.Reset();
        Assert.Equal(TimeSpan.FromSeconds(10), backoff.NextDelay());
    }

    [Fact]
    public void FailureCounter_trips_on_fifth_failure_within_window()
    {
        var counter = new FailureCounter(5, TimeSpan.FromSeconds(30));
        for (int i = 0; i < 4; i++) Assert.False(counter.Record(TimeSpan.FromSeconds(i)));
        Assert.True(counter.Record(TimeSpan.FromSeconds(4)));
    }

    [Fact]
    public void FailureCounter_forgets_old_failures()
    {
        var counter = new FailureCounter(5, TimeSpan.FromSeconds(30));
        for (int i = 0; i < 5; i++) Assert.False(counter.Record(TimeSpan.FromSeconds(i * 10)));
    }
}
