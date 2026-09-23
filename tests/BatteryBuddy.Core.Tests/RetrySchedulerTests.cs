using BatteryBuddy.Core.Retry;

namespace BatteryBuddy.Core.Tests;

public class RetrySchedulerTests
{
    static readonly TimeSpan Stable = TimeSpan.FromSeconds(30);
    static readonly TimeSpan Soon = TimeSpan.FromSeconds(2);

    static Backoff NewBackoff() => new(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));

    [Fact]
    public async Task Wake_that_arrives_before_waiting_is_not_lost()
    {
        var scheduler = new RetryScheduler(new Backoff(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)), Stable);
        scheduler.Wake();
        var wait = scheduler.WaitAsync(CancellationToken.None);
        Assert.Same(wait, await Task.WhenAny(wait, Task.Delay(Soon)));
    }

    [Fact]
    public async Task Wake_during_wait_ends_it()
    {
        var scheduler = new RetryScheduler(new Backoff(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)), Stable);
        var wait = scheduler.WaitAsync(CancellationToken.None);
        scheduler.Wake();
        Assert.Same(wait, await Task.WhenAny(wait, Task.Delay(Soon)));
    }

    [Fact]
    public async Task A_consumed_wake_does_not_skip_the_next_delay()
    {
        var scheduler = new RetryScheduler(new Backoff(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)), Stable);
        scheduler.Wake();
        await scheduler.WaitAsync(CancellationToken.None);
        var second = scheduler.WaitAsync(CancellationToken.None);
        Assert.NotSame(second, await Task.WhenAny(second, Task.Delay(TimeSpan.FromMilliseconds(300))));
    }

    [Fact]
    public void Short_session_keeps_backing_off()
    {
        var backoff = NewBackoff();
        var scheduler = new RetryScheduler(backoff, Stable);
        backoff.NextDelay();
        backoff.NextDelay();
        scheduler.SessionEnded(TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(40), backoff.NextDelay());
    }

    [Fact]
    public void Stable_session_resets_backoff()
    {
        var backoff = NewBackoff();
        var scheduler = new RetryScheduler(backoff, Stable);
        backoff.NextDelay();
        backoff.NextDelay();
        scheduler.SessionEnded(TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.FromSeconds(10), backoff.NextDelay());
    }

    [Fact]
    public void Wake_with_reset_restarts_backoff()
    {
        var backoff = NewBackoff();
        var scheduler = new RetryScheduler(backoff, Stable);
        backoff.NextDelay();
        backoff.NextDelay();
        scheduler.Wake(resetBackoff: true);
        Assert.Equal(TimeSpan.FromSeconds(10), backoff.NextDelay());
    }

    [Fact]
    public async Task Cancellation_ends_wait()
    {
        var scheduler = new RetryScheduler(new Backoff(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)), Stable);
        using var cts = new CancellationTokenSource();
        var wait = scheduler.WaitAsync(cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }
}
