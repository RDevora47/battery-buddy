using BatteryBuddy.Core.Devices;

namespace BatteryBuddy.Core.Tracking;

/// <summary>Merges per-source snapshots into one device list. Not thread-safe: use from the UI thread.</summary>
public sealed class DeviceRegistry
{
    readonly Dictionary<string, Dictionary<string, DeviceReading>> _bySource = new();
    readonly Dictionary<string, DeviceReading> _merged = new();

    public event EventHandler<RegistryChange>? Changed;

    public IReadOnlyList<DeviceReading> Connected =>
        _merged.Values.Where(r => r.IsConnected).OrderBy(r => r.Key, StringComparer.Ordinal).ToList();

    public DeviceReading? LastKnown(string key) => _merged.GetValueOrDefault(key);

    public void Apply(string source, IReadOnlyList<DeviceReading> snapshot)
    {
        _bySource[source] = snapshot.GroupBy(r => r.Key).ToDictionary(g => g.Key, g => g.Last());

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
                    var gone = previous! with { IsConnected = false };
                    _merged[key] = gone;
                    removed.Add(gone);
                }
                else if (next is not null)
                {
                    _merged.TryAdd(key, next);
                }
                continue;
            }

            _merged[key] = next;
            if (!wasConnected) added.Add(next);
            else if (!SameContent(previous!, next)) updated.Add(next);
        }

        var change = new RegistryChange(added, updated, removed);
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
