using BatteryBuddy.Core.Ui;

namespace BatteryBuddy.Core.Tests;

public class WindowPlacementTests
{
    static readonly ScreenRect Primary = new(0, 0, 1920, 1032);
    static readonly ScreenRect Secondary = new(1920, 0, 1920, 1040);

    [Fact]
    public void No_saved_position_uses_bottom_right_above_taskbar() =>
        Assert.Equal((1692.0, 724.0), WindowPlacement.Resolve(null, 220, 300, new[] { Primary }, Primary));

    [Fact]
    public void Saved_position_on_screen_is_kept() =>
        Assert.Equal((100.0, 200.0), WindowPlacement.Resolve((100, 200), 220, 300, new[] { Primary }, Primary));

    [Fact]
    public void Saved_position_on_secondary_monitor_is_kept() =>
        Assert.Equal((2500.0, 300.0), WindowPlacement.Resolve((2500, 300), 220, 300, new[] { Primary, Secondary }, Primary));

    [Fact]
    public void Saved_position_on_missing_monitor_resets_to_default() =>
        Assert.Equal((1692.0, 724.0), WindowPlacement.Resolve((2500, 300), 220, 300, new[] { Primary }, Primary));

    [Fact]
    public void Mostly_offscreen_position_resets() =>
        Assert.Equal((1692.0, 724.0), WindowPlacement.Resolve((1800, 900), 220, 300, new[] { Primary }, Primary));
}
