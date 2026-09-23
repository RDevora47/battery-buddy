using BatteryBuddy.Backends.Windows.Pnp;

namespace BatteryBuddy.Tests;

public class VersionedStoreTests
{
    static Dictionary<string, string> Contents(VersionedStore<string> store) =>
        store.Select((id, value) => (id, value)).ToDictionary(p => p.id, p => p.value);

    [Fact]
    public void Snapshot_replaces_contents_and_drops_stale_entries()
    {
        var store = new VersionedStore<string>();
        store.Upsert("a", "old");
        store.Upsert("gone", "x");
        long version = store.Version;
        store.ReplaceAll(version, new[] { ("a", "new"), ("c", "c") });
        Assert.Equal(new Dictionary<string, string> { ["a"] = "new", ["c"] = "c" }, Contents(store));
    }

    [Fact]
    public void Snapshot_does_not_overwrite_an_update_made_while_it_was_read()
    {
        var store = new VersionedStore<string>();
        store.Upsert("a", "old");
        long version = store.Version;           // FindAllAsync starts here
        store.Upsert("a", "watcher-update");    // watcher delta lands meanwhile
        store.ReplaceAll(version, new[] { ("a", "old-snapshot") });
        Assert.Equal("watcher-update", Contents(store)["a"]);
    }

    [Fact]
    public void Snapshot_does_not_drop_an_entry_added_while_it_was_read()
    {
        var store = new VersionedStore<string>();
        long version = store.Version;
        store.Upsert("b", "added");
        store.ReplaceAll(version, Array.Empty<(string, string)>());
        Assert.True(Contents(store).ContainsKey("b"));
    }

    [Fact]
    public void Snapshot_does_not_resurrect_an_entry_removed_while_it_was_read()
    {
        var store = new VersionedStore<string>();
        store.Upsert("a", "x");
        long version = store.Version;
        store.Remove("a");
        store.ReplaceAll(version, new[] { ("a", "x") });
        Assert.Empty(Contents(store));
    }

    [Fact]
    public void Update_applies_to_existing_entries_only()
    {
        var store = new VersionedStore<List<int>>();
        store.Upsert("a", new List<int>());
        Assert.True(store.TryUpdate("a", l => l.Add(1)));
        Assert.False(store.TryUpdate("missing", l => l.Add(1)));
        Assert.Equal(new[] { 1 }, store.Select((_, l) => l.Count).ToArray());
    }
}
