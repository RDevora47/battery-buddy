using BatteryBuddy.Devices;

namespace BatteryBuddy.Backend;

/// <summary>
/// Merges per-source snapshots into one device list: readings share a device when their keys match or
/// their hardware addresses do (the first name seen for an address wins); a reading with a battery beats one
/// without. Charging comes from the source
/// when it reports it, else it's inferred. Not thread-safe: <see cref="DeviceHub"/> serializes calls.
/// </summary>
public sealed class DeviceRegistry
{
    readonly Dictionary<string, Dictionary<string, DeviceReading>> _bySource = new();
    readonly Dictionary<string, DeviceReading> _merged = new();
    readonly Dictionary<string, (string Key, string Name)> _byAddress = new(StringComparer.OrdinalIgnoreCase);
    readonly ChargeTracker _charge;
    readonly DrainHistory? _drain;
    readonly Func<DeviceReading, bool> _detailExpected;

    /// <param name="detailExpected">Whether some source normally provides <see cref="DeviceReading.Detail"/> for a device.</param>
    /// <param name="drain">Where to record how fast merged readings drain, if anywhere.</param>
    public DeviceRegistry(ChargeTracker? charge = null, Func<DeviceReading, bool>? detailExpected = null, DrainHistory? drain = null)
    {
        _charge = charge ?? new ChargeTracker();
        _drain = drain;
        _detailExpected = detailExpected ?? (_ => false);
    }

    public event EventHandler<DeviceChange>? Changed;

    public ChargeTracker Charge => _charge;

    public IReadOnlyList<DeviceReading> Connected =>
        _merged.Values.Where(r => r.IsConnected).OrderBy(r => r.Key, StringComparer.Ordinal).ToList();

    public DeviceReading? LastKnown(string key) => _merged.GetValueOrDefault(key);

    /// <summary>
    /// When the earliest inferred-charging flag still ahead of <paramref name="now"/> runs out; re-check with
    /// <see cref="Refresh"/> then. One already past can't change anything: either Refresh has cleared that flag, or
    /// the device is charging for another reason (its source says so, or buds sit in their case).
    /// </summary>
    public DateTimeOffset? NextChargeExpiry(DateTimeOffset now) =>
        _merged.Values.Where(r => r.IsConnected && r.IsCharging && !r.ChargingKnown)
                      .Select(r => _charge.ChargingUntil(r.Key))
                      .Where(until => until > now)
                      .Min();

    public void Apply(string source, IReadOnlyList<DeviceReading> snapshot)
    {
        _bySource[source] = snapshot.Select(Canonical).GroupBy(r => r.Key).ToDictionary(g => g.Key, g => g.Last());
        foreach (var reading in _bySource[source].Values)
            if (reading.IsConnected && !reading.BatteryStale && reading.EffectiveBattery is int level)
                // Only plain level readings pace like a periodic reporter; buds and charge-aware sources push.
                _charge.Observe(reading.Key, source, level, reading.ReadAt,
                    periodic: reading.Detail is null && !reading.ChargingKnown);

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
                    _drain?.Interrupt(key);
                }
                else if (next is not null)
                {
                    _merged.TryAdd(key, next);
                }
                continue;
            }

            next = WithCharging(next, next.ReadAt) with { DetailUnavailable = next.Detail is null && _detailExpected(next) };
            _merged[key] = next;
            // Only fresh readings: another source's untouched one would restart a stretch at an old time.
            if (next.Source == source) _drain?.Observe(key, DrainSource(next), DrainingLevel(next), next.ReadAt);
            if (!wasConnected) added.Add(next);
            else if (!SameContent(previous!, next)) updated.Add(next);
        }

        Raise(new DeviceChange(added, updated, removed));
    }

    DeviceReading Canonical(DeviceReading reading)
    {
        if (reading.Address is not string address) return reading;
        if (_byAddress.TryGetValue(address, out var known))
            return reading with { Key = known.Key, Name = known.Name };
        _byAddress[address] = (reading.Key, reading.Name);
        return reading;
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
        Raise(new DeviceChange(Array.Empty<DeviceReading>(), updated, Array.Empty<DeviceReading>()));
    }

    /// <summary>The level to measure drain from: only while in use on battery (buds: one worn, the lower bud out of the case).</summary>
    static int? DrainingLevel(DeviceReading reading) =>
        reading.BatteryStale || reading.NoBattery || reading.Detail is { LeftWorn: false, RightWorn: false }
            ? null
            : reading.UnchargedBattery;

    // Which buds are measured is part of the stretch: taking the other one out of the case changes what
    // "the lower bud" is, and that jump isn't drain.
    static string DrainSource(DeviceReading reading) =>
        reading.Detail is { } buds ? $"{reading.Source}:{(buds.LeftInCase ? "" : "L")}{(buds.RightInCase ? "" : "R")}" : reading.Source;

    DeviceReading WithCharging(DeviceReading reading, DateTimeOffset now) =>
        reading.ChargingKnown ? reading
            : reading with { IsCharging = reading.Detail?.AnyInCase == true || _charge.IsCharging(reading.Key, now) };

    void Raise(DeviceChange change)
    {
        if (!change.IsEmpty) Changed?.Invoke(this, change);
    }

    static DeviceReading? Pick(IEnumerable<DeviceReading> candidates)
    {
        var list = candidates.ToList();
        return list.Where(r => r.IsConnected)
                   .OrderByDescending(r => !r.NoBattery)
                   .ThenByDescending(r => r.Detail is not null)
                   .ThenByDescending(r => r.ChargingKnown)
                   .ThenByDescending(r => r.ReadAt)
                   .FirstOrDefault()
            ?? list.OrderByDescending(r => r.ReadAt).FirstOrDefault();
    }

    static bool SameContent(DeviceReading a, DeviceReading b) =>
        a with { ReadAt = b.ReadAt, Source = b.Source } == b;
}
