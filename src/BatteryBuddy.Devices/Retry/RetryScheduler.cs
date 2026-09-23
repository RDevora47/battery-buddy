namespace BatteryBuddy.Devices.Retry;

/// <summary>
/// Paces reconnect attempts. A wake that arrives at any time (even mid-attempt) ends the next wait,
/// and the backoff only resets after a session that stayed up for <c>stableSession</c>.
/// </summary>
public sealed class RetryScheduler
{
    readonly Backoff _backoff;
    readonly TimeSpan _stableSession;
    readonly object _gate = new();
    TaskCompletionSource _wake = NewSignal();
    bool _wakePending;

    public RetryScheduler(Backoff backoff, TimeSpan stableSession)
    {
        _backoff = backoff;
        _stableSession = stableSession;
    }

    /// <summary>Thread-safe. Ends the current or next wait.</summary>
    public void Wake(bool resetBackoff = false)
    {
        lock (_gate)
        {
            if (resetBackoff) _backoff.Reset();
            _wakePending = true;
            _wake.TrySetResult();
        }
    }

    public void SessionEnded(TimeSpan linkDuration)
    {
        if (linkDuration < _stableSession) return;
        lock (_gate) _backoff.Reset();
    }

    public async Task WaitAsync(CancellationToken ct)
    {
        Task wake;
        TimeSpan delay;
        lock (_gate)
        {
            if (_wakePending)
            {
                _wakePending = false;
                _wake = NewSignal();
                return;
            }
            delay = _backoff.NextDelay();
            wake = _wake.Task;
        }

        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await Task.WhenAny(wake, Task.Delay(delay, delayCts.Token)).ConfigureAwait(false);
        delayCts.Cancel();

        lock (_gate)
        {
            if (_wakePending)
            {
                _wakePending = false;
                _wake = NewSignal();
            }
        }
        ct.ThrowIfCancellationRequested();
    }

    static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
