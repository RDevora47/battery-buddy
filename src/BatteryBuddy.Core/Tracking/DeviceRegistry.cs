using BatteryBuddy.Core.Devices;

namespace BatteryBuddy.Core.Tracking;

/// <summary>Merges per-source snapshots into one device list. Not thread-safe: use from the UI thread.</summary>
public sealed class DeviceRegistry
{
    readonly Dictionary<string, Dictionary<string, DeviceReading>> _bySource = new();
    readonly Dictionary<string, DeviceReading> _merged = new();
    readonly ChargeTracker _charge;

    public DeviceRegistry(ChargeTracker? charge = null) => _charge = charge ?? new ChargeTracker();

    public event EventHandler<RegistryChange>? Changed;

    public ChargeTracker Charge => _charge;

    public IReadOnlyList<DeviceReading> Connected =>
        _merged.Values.Where(r => r.IsConnected).OrderBy(r => r.Key, StringComparer.Ordinal).ToList();

    public DeviceReading? LastKnown(string key) => _merged.GetValueOrDefault(key);

    /// <summary>When the earliest inferred-charging flag runs out; re-check with <see cref="Refresh"/> then.</summary>
    public DateTimeOffset? NextChargeExpiry =>
        _merged.Values.Where(r => r.IsConnected && r.IsCharging)
                      .Select(r => _charge.ChargingUntil(r.Key))
                      .Where(until => until is not null)
                      .Min();

    public void Apply(string source, IReadOnlyList<DeviceReading> snapshot)
    {
        _bySource[source] = snapshot.GroupBy(r => r.Key).ToDictionary(g => g.Key, g => g.Last());
        foreach (var reading in _bySource[source].Values)
            if (reading.IsConnected && reading.EffectiveBattery is int level)
                _charge.Observe(reading.Key, source, level, reading.ReadAt);

        var added = new List<DeviceReading>();
        var updated = new List<DeviceReading>();
        var removed = new List<DeviceReading>();
        var keys = _bySource.Values.SelectMany(d => d.Keys).Concat(_merged.Keys).Distinct().ToList();

        foreach (var key in keys)
        {
            _merged.TryGetValue(key, out var previous);
            bool wasConnected = previous?.IsConnected == true;
            var next = Pick(_bySource.Values.Select(d => d.GetValueOrDefault(key)).OfType<DeviceReading>());

            if (next is null || !next.IsConnected)
            {
                if (wasConnected)
                {
                    var gone = previous! with { IsConnected = false, IsCharging = false };
                    _merged[key] = gone;
                    removed.Add(gone);
                    _charge.Forget(key);
                }
                else if (next is not null)
                {
                    _merged.TryAdd(key, next);
                }
                continue;
            }

            next = WithCharging(next, next.ReadAt);
            _merged[key] = next;
            if (!wasConnected) added.Add(next);
            else if (!SameContent(previous!, next)) updated.Add(next);
        }

        Raise(new RegistryChange(added, updated, removed));
    }

    /// <summary>Re-evaluates inferred charging against the clock (a bolt can run out without a new reading).</summary>
    public void Refresh(DateTimeOffset now)
    {
        var updated = new List<DeviceReading>();
        foreach (var reading in Connected)
        {
            var next = WithCharging(reading, now);
            if (next.IsCharging == reading.IsCharging) continue;
            _merged[reading.Key] = next;
            updated.Add(next);
        }
        Raise(new RegistryChange(Array.Empty<DeviceReading>(), updated, Array.Empty<DeviceReading>()));
    }

    DeviceReading WithCharging(DeviceReading reading, DateTimeOffset now) =>
        reading with { IsCharging = reading.Detail?.AnyInCase == true || _charge.IsCharging(reading.Key, now) };

    void Raise(RegistryChange change)
    {
        if (!change.IsEmpty) Changed?.Invoke(this, change);
    }

    static DeviceReading? Pick(IEnumerable<DeviceReading> candidates)
    {
        var list = candidates.ToList();
        return list.Where(r => r.IsConnected)
                   .OrderByDescending(r => r.Detail is not null)
                   .ThenByDescending(r => r.ReadAt)
                   .FirstOrDefault()
            ?? list.OrderByDescending(r => r.ReadAt).FirstOrDefault();
    }

    static bool SameContent(DeviceReading a, DeviceReading b) =>
        a with { ReadAt = b.ReadAt, Source = b.Source } == b;
}
