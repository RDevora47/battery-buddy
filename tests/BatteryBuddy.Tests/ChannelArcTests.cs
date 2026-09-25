using BatteryBuddy.Pet.Scene;

namespace BatteryBuddy.Tests;

public class ChannelArcTests
{
    [Fact]
    public void Three_channels_sit_left_top_right_of_the_mouse()
    {
        var arc = ChannelArc.Positions(new PixelPoint(30, 34), 3, 48);
        Assert.Equal(new[] { new PixelPoint(19, 26), new PixelPoint(30, 19), new PixelPoint(41, 26) }, arc);
    }

    [Fact]
    public void Two_channels_sit_left_and_right()
    {
        var arc = ChannelArc.Positions(new PixelPoint(30, 34), 2, 48);
        Assert.Equal(new[] { new PixelPoint(19, 26), new PixelPoint(41, 26) }, arc);
    }

    [Fact]
    public void Arc_shifts_inside_the_frame_at_either_edge()
    {
        // A mouse in a float slot at the left edge, and one hard against the right edge.
        var left = ChannelArc.Positions(new PixelPoint(0, 29), 3, 48);
        Assert.Equal(0, left.Min(p => p.X));
        Assert.Equal(new[] { 0, 11, 22 }, left.Select(p => p.X));

        var right = ChannelArc.Positions(new PixelPoint(40, 34), 3, 48);
        Assert.Equal(48 - ChannelArc.BubbleSize, right.Max(p => p.X));
    }

    [Fact]
    public void Bubbles_never_go_above_the_frame() =>
        Assert.All(ChannelArc.Positions(new PixelPoint(20, 5), 3, 48), p => Assert.True(p.Y >= 0));

    [Fact]
    public void Required_width_leaves_room_for_the_right_bubble() =>
        Assert.Equal(48, ChannelArc.RequiredWidth(new PixelPoint(30, 34)));
}
