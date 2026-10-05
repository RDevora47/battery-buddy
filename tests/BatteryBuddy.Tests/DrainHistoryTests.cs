using BatteryBuddy.Backend;

namespace BatteryBuddy.Tests;

public class DrainHistoryTests
{
    const string Key = "buds3 pro de roberto";
    const string Buds = "galaxybuds";

    readonly DrainHistory _history = new();

    static DateTimeOffset At(double minutes) => TestReadings.T0.AddMinutes(minutes);

    [Fact]
    public void Time_is_measured_from_the_first_change_not_the_first_reading()
    {
        _history.Observe(Key, Buds, 80, At(0));
        _history.Observe(Key, Buds, 79, At(2));    // when it reached 80 is unknown: no segment yet
        Assert.Empty(_history.Segments(Key));
        _history.Observe(Key, Buds, 77, At(8));
        Assert.Equal(new DrainSegment(At(2), At(8), 79, 77), Assert.Single(_history.Segments(Key)));
    }

    [Fact]
    public void Repeated_levels_change_nothing()
    {
        _history.Observe(Key, Buds, 80, At(0));
        _history.Observe(Key, Buds, 79, At(2));
        _history.Observe(Key, Buds, 79, At(4));
        _history.Observe(Key, Buds, 78, At(6));
        Assert.Equal(new DrainSegment(At(2), At(6), 79, 78), Assert.Single(_history.Segments(Key)));
    }

    [Theory]
    [InlineData("rise")]
    [InlineData("no level")]
    [InlineData("other source")]
    [InlineData("interrupt")]
    [InlineData("interrupt all")]
    public void A_break_starts_a_new_stretch(string cause)
    {
        _history.Observe(Key, Buds, 80, At(0));
        _history.Observe(Key, Buds, 79, At(2));
        switch (cause)
        {
            case "rise": _history.Observe(Key, Buds, 81, At(3)); break;
            case "no level": _history.Observe(Key, Buds, null, At(3)); break;
            case "other source": _history.Observe(Key, "windows", 79, At(3)); break;
            case "interrupt": _history.Interrupt(Key); break;
            case "interrupt all": _history.InterruptAll(); break;
        }
        _history.Observe(Key, Buds, 78, At(30));
        Assert.Empty(_history.Segments(Key));
    }

    [Fact]
    public void Level_since_is_the_last_change_or_the_stretch_start()
    {
        Assert.Null(_history.LevelSince(Key));
        _history.Observe(Key, Buds, 80, At(0));
        Assert.Equal(At(0), _history.LevelSince(Key));
        _history.Observe(Key, Buds, 79, At(5));
        Assert.Equal(At(5), _history.LevelSince(Key));
        _history.Interrupt(Key);
        Assert.Null(_history.LevelSince(Key));
    }

    [Fact]
    public void Segments_older_than_a_week_are_dropped()
    {
        var history = new DrainHistory(new Dictionary<string, List<DrainSegment>>
        {
            [Key] = new() { new(At(-8 * 24 * 60 - 10), At(-8 * 24 * 60), 50, 49) },
            ["mx master 3s"] = new() { new(At(-8 * 24 * 60 - 10), At(-8 * 24 * 60), 50, 49) },
        });
        history.Observe(Key, Buds, 80, At(0));
        history.Observe(Key, Buds, 79, At(1));
        history.Observe(Key, Buds, 78, At(2));
        Assert.Equal(new DrainSegment(At(1), At(2), 79, 78), Assert.Single(history.Segments(Key)));
        Assert.Empty(history.Segments("mx master 3s"));
    }

    [Fact]
    public void Changed_is_raised_for_each_new_segment()
    {
        int changes = 0;
        _history.Changed += () => changes++;
        _history.Observe(Key, Buds, 80, At(0));
        _history.Observe(Key, Buds, 79, At(1));
        Assert.Equal(0, changes);
        _history.Observe(Key, Buds, 78, At(2));
        _history.Observe(Key, Buds, 77, At(3));
        Assert.Equal(2, changes);
    }
}
