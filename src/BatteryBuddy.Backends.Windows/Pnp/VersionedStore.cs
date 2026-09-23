namespace BatteryBuddy.Backends.Windows.Pnp;

/// <summary>
/// Thread-safe id → value map that lets a slow full snapshot be merged without clobbering
/// incremental changes (watcher events) that happened while the snapshot was being read.
/// </summary>
public sealed class VersionedStore<T>
{
    readonly object _gate = new();
    readonly Dictionary<string, (T Value, long Version)> _items = new();
    readonly Dictionary<string, long> _removedAt = new();
    long _version;

    /// <summary>Read before starting a snapshot; pass it to <see cref="ReplaceAll"/>.</summary>
    public long Version
    {
        get { lock (_gate) return _version; }
    }

    public void Upsert(string id, T value)
    {
        lock (_gate)
        {
            _items[id] = (value, ++_version);
            _removedAt.Remove(id);
        }
    }

    public bool TryUpdate(string id, Action<T> update)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(id, out var entry)) return false;
            update(entry.Value);
            _items[id] = (entry.Value, ++_version);
            return true;
        }
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            if (!_items.Remove(id)) return false;
            _removedAt[id] = ++_version;
            return true;
        }
    }

    /// <summary>Applies a snapshot taken at <paramref name="snapshotVersion"/>, keeping anything changed since.</summary>
    public void ReplaceAll(long snapshotVersion, IEnumerable<(string Id, T Value)> snapshot)
    {
        lock (_gate)
        {
            var seen = new HashSet<string>();
            foreach (var (id, value) in snapshot)
            {
                seen.Add(id);
                if (_items.TryGetValue(id, out var entry) && entry.Version > snapshotVersion) continue;
                if (_removedAt.TryGetValue(id, out var removed) && removed > snapshotVersion) continue;
                _items[id] = (value, ++_version);
            }
            foreach (var id in _items.Keys.ToList())
                if (!seen.Contains(id) && _items[id].Version <= snapshotVersion)
                    _items.Remove(id);
            _removedAt.Clear();
        }
    }

    /// <summary>Maps every entry under the lock, so values aren't read while another thread updates them.</summary>
    public IReadOnlyList<TOut> Select<TOut>(Func<string, T, TOut> map)
    {
        lock (_gate) return _items.Select(kv => map(kv.Key, kv.Value.Value)).ToList();
    }
}
