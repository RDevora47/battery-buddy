namespace BatteryBuddy.Backend;

/// <summary>A device's level fell from <paramref name="From"/> to <paramref name="To"/> between two level changes.</summary>
public sealed record DrainSegment(DateTimeOffset Start, DateTimeOffset End, int From, int To);

/// <summary>
/// Records how fast each device drains while it's in use, for <see cref="TimeLeftEstimator"/>. A stretch runs while
/// the device is connected, not charging and its level never rises; anything else (a null level, a rise, another
/// source, <see cref="Interrupt"/>) ends it, so sleep and disconnected time are never counted. Time is measured
/// between level changes, never from the first reading, so coarse levels don't undercount the drop.
/// Keeps a week of segments. Not thread-safe: <see cref="DeviceHub"/> serializes calls.
/// </summary>
public sealed class DrainHistory
{
    public static readonly TimeSpan Kept = TimeSpan.FromDays(7);

    sealed record Stretch(string Source, int Level, DateTimeOffset StartedAt, DateTimeOffset? ChangedAt);

    readonly Dictionary<string, List<DrainSegment>> _segments;
    readonly Dictionary<string, Stretch> _stretches = new();

    public DrainHistory(IReadOnlyDictionary<string, List<DrainSegment>>? saved = null)
    {
        _segments = saved?.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()) ?? new();
    }

    /// <summary>Everything kept, for saving.</summary>
    public IReadOnlyDictionary<string, List<DrainSegment>> All => _segments;

    /// <summary>Raised when a segment is added, so the history can be saved.</summary>
    public event Action? Changed;

    public IReadOnlyList<DrainSegment> Segments(string key) =>
        _segments.TryGetValue(key, out var list) ? list : Array.Empty<DrainSegment>();

    /// <summary>Since when the device has been at its current level in this stretch (or since the stretch began); null outside one.</summary>
    public DateTimeOffset? LevelSince(string key) =>
        _stretches.TryGetValue(key, out var stretch) ? stretch.ChangedAt ?? stretch.StartedAt : null;

    /// <param name="level">The level to measure, or null when the device isn't draining in use (charging, not worn…).</param>
    public void Observe(string key, string source, int? level, DateTimeOffset at)
    {
        if (level is not int now)
        {
            Interrupt(key);
            return;
        }
        if (!_stretches.TryGetValue(key, out var stretch) || stretch.Source != source || now > stretch.Level)
        {
            _stretches[key] = new Stretch(source, now, at, null);
            return;
        }
        if (now == stretch.Level) return;

        _stretches[key] = stretch with { Level = now, ChangedAt = at };
        if (stretch.ChangedAt is DateTimeOffset since && at > since)
        {
            Prune(at - Kept);
            if (!_segments.TryGetValue(key, out var list)) _segments[key] = list = new();
            list.Add(new DrainSegment(since, at, stretch.Level, now));
            Changed?.Invoke();
        }
    }

    /// <summary>The device stopped being measured (disconnected): its stretch ends here.</summary>
    public void Interrupt(string key) => _stretches.Remove(key);

    /// <summary>The PC is going to sleep: every stretch ends here.</summary>
    public void InterruptAll() => _stretches.Clear();

    void Prune(DateTimeOffset cutoff)
    {
        foreach (var (key, list) in _segments.ToList())
        {
            list.RemoveAll(s => s.End < cutoff);
            if (list.Count == 0) _segments.Remove(key);
        }
    }
}
