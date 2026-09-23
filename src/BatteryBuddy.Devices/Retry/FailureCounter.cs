namespace BatteryBuddy.Devices.Retry;

/// <summary>Trips when <c>threshold</c> failures happen within <c>window</c>; resets after tripping.</summary>
public sealed class FailureCounter
{
    readonly int _threshold;
    readonly TimeSpan _window;
    readonly Queue<TimeSpan> _failures = new();

    public FailureCounter(int threshold, TimeSpan window)
    {
        _threshold = threshold;
        _window = window;
    }

    public bool Record(TimeSpan now)
    {
        _failures.Enqueue(now);
        while (_failures.Count > 0 && now - _failures.Peek() > _window) _failures.Dequeue();
        if (_failures.Count < _threshold) return false;
        _failures.Clear();
        return true;
    }
}
