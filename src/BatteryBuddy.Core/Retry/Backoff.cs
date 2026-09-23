namespace BatteryBuddy.Core.Retry;

public sealed class Backoff
{
    readonly TimeSpan _initial;
    readonly TimeSpan _max;
    TimeSpan _next;

    public Backoff(TimeSpan initial, TimeSpan max)
    {
        _initial = initial;
        _max = max;
        _next = initial;
    }

    public TimeSpan NextDelay()
    {
        var delay = _next;
        _next = TimeSpan.FromTicks(Math.Min(_max.Ticks, _next.Ticks * 2));
        return delay;
    }

    public void Reset() => _next = _initial;
}
