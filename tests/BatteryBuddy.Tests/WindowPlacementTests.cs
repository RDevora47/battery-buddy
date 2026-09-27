using BatteryBuddy.Pet.Ui;

namespace BatteryBuddy.Tests;

public class WindowPlacementTests
{
    static readonly ScreenRect Primary = new(0, 0, 1920, 1032);
    static readonly ScreenRect Secondary = new(1920, 0, 1920, 1040);
    // Pet image inside the window: room for the bubble around it.
    static readonly ScreenRect Pet = new(44, 140, 176, 176);

    static (double, double) Resolve((double, double)? saved, params ScreenRect[] areas) =>
        WindowPlacement.Resolve(saved, Pet, areas, Primary);

    [Fact]
    public void No_saved_position_puts_pet_bottom_right_above_taskbar() =>
        Assert.Equal((1692.0, 708.0), Resolve(null, Primary));

    [Fact]
    public void Saved_position_on_screen_is_kept() =>
        Assert.Equal((100.0, 200.0), Resolve((100, 200), Primary));

    [Fact]
    public void Saved_position_on_secondary_monitor_is_kept() =>
        Assert.Equal((2500.0, 300.0), Resolve((2500, 300), Primary, Secondary));

    [Fact]
    public void Saved_position_on_missing_monitor_resets_to_default() =>
        Assert.Equal((1692.0, 708.0), Resolve((2500, 300), Primary));

    [Fact]
    public void Pet_flush_with_the_corner_is_kept_even_though_the_window_hangs_off() =>
        Assert.Equal((-44.0, -140.0), Resolve((-44, -140), Primary));

    [Fact]
    public void Pet_half_off_the_right_edge_is_kept() =>
        Assert.Equal((1788.0, 500.0), Resolve((1788, 500), Primary));

    [Fact]
    public void Pet_half_off_the_top_is_kept() =>
        Assert.Equal((100.0, -228.0), Resolve((100, -228), Primary));

    [Fact]
    public void Pet_half_over_the_taskbar_is_kept() =>
        Assert.Equal((100.0, 804.0), Resolve((100, 804), Primary));

    [Fact]
    public void Pet_more_than_half_off_screen_resets_to_default() =>
        Assert.Equal((1692.0, 708.0), Resolve((100, -300), Primary));
}

public class ScreenClampTests
{
    static readonly ScreenRect Left = new(0, 0, 1920, 1032);
    static readonly ScreenRect Right = new(1920, 0, 1920, 1040);

    [Fact]
    public void Rect_inside_needs_no_offset() =>
        Assert.Equal((0.0, 0.0), ScreenClamp.Into(new(10, 10, 100, 100), Left));

    [Fact]
    public void Rect_is_pushed_in_from_every_side()
    {
        Assert.Equal((5.0, 0.0), ScreenClamp.Into(new(-5, 10, 100, 100), Left));
        Assert.Equal((-30.0, 0.0), ScreenClamp.Into(new(1850, 10, 100, 100), Left));
        Assert.Equal((0.0, 7.0), ScreenClamp.Into(new(10, -7, 100, 100), Left));
        Assert.Equal((0.0, -18.0), ScreenClamp.Into(new(10, 950, 100, 100), Left));
    }

    [Fact]
    public void Rect_bigger_than_the_area_is_pinned_top_left() =>
        Assert.Equal((10.0, 20.0), ScreenClamp.Into(new(-10, -20, 3000, 2000), Left));

    [Fact]
    public void Overhang_lets_that_fraction_of_the_rect_stick_out()
    {
        Assert.Equal((0.0, 0.0), ScreenClamp.Into(new(-50, 10, 100, 100), Left, 0.5));
        Assert.Equal((10.0, 0.0), ScreenClamp.Into(new(-60, 10, 100, 100), Left, 0.5));
        Assert.Equal((0.0, 20.0), ScreenClamp.Into(new(10, -70, 100, 100), Left, 0.5));
        Assert.Equal((-30.0, 0.0), ScreenClamp.Into(new(1900, 10, 100, 100), Left, 0.5));
        Assert.Equal((0.0, -18.0), ScreenClamp.Into(new(10, 1000, 100, 100), Left, 0.5));
    }

    [Fact]
    public void Area_containing_the_point_wins() =>
        Assert.Equal(Right, ScreenClamp.AreaFor(2000, 500, new[] { Left, Right }));

    [Fact]
    public void Point_outside_every_area_uses_the_nearest()
    {
        Assert.Equal(Right, ScreenClamp.AreaFor(4000, 500, new[] { Left, Right }));
        Assert.Equal(Left, ScreenClamp.AreaFor(-50, 1100, new[] { Left, Right }));
    }
}

public class BubblePlacementTests
{
    static readonly ScreenRect Area = new(0, 0, 1920, 1032);

    [Fact]
    public void Bubble_sits_above_the_pet_right_aligned() =>
        Assert.Equal((1000.0 + 176 - 4 - 150, 500.0 - 6 - 80),
            BubblePlacement.Resolve(new(1000, 500, 176, 176), 150, 80, Area));

    [Fact]
    public void Bubble_goes_below_a_pet_at_the_top_of_the_screen() =>
        Assert.Equal((1022.0, 20.0 + 176 + 6),
            BubblePlacement.Resolve(new(1000, 20, 176, 176), 150, 80, Area));

    [Fact]
    public void Wide_bubble_is_shifted_right_at_the_left_edge() =>
        Assert.Equal((0.0, 414.0), BubblePlacement.Resolve(new(0, 500, 176, 176), 216, 80, Area));
}
