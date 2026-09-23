using BatteryBuddy.Core.Devices;
using BatteryBuddy.Core.Scene;

namespace BatteryBuddy.Core.Tests;

public class FrameComposerTests
{
    const uint Red = 0xFFFF0000, Blue = 0xFF0000FF;

    // 20x12 canvas; 2x2 body at (0,0); a 1x1 "mouse" at hands (5,5) that follows the body;
    // a 3x1 "keyboard" seat at (0,1) under the body.
    static readonly SkinLayout Layout = new(
        20, 12, new PixelPoint(0, 0),
        new Dictionary<string, Place>
        {
            ["hands"] = new(new[] { new PixelPoint(5, 5) }, new PixelPoint(0, 8), true),
            ["seat"] = new(new[] { new PixelPoint(0, 1) }, new PixelPoint(0, 10), false),
        },
        new Dictionary<string, PixelPoint>());

    static readonly Dictionary<string, Sprite> Sprites = new()
    {
        ["body"] = new("body", 2, 2, new[] { Red, Red, Red, Red }),
        ["mouse"] = new("mouse", 1, 1, new[] { Blue }),
        ["keyboard"] = new("keyboard", 3, 1, new[] { Blue, Blue, Blue }),
        ["fx"] = new("fx", 1, 1, new[] { Blue }),
    };

    static FrameSpec Spec(int dy = 0, bool criticalVisible = true, params Overlay[] overlays) =>
        new("body", dy, overlays, criticalVisible);

    static DevicePlacement Mouse(int battery = 80) =>
        new(TestReadings.Make("Mouse", DeviceKind.Mouse, battery: battery), "hands", "mouse");

    static uint Pixel(ComposedFrame f, int x, int y) => f.Pixels[y * f.Width + x];

    [Fact]
    public void Hit_test_distinguishes_body_device_and_empty()
    {
        var frame = FrameComposer.Compose(Layout, Sprites, new[] { Mouse() }, Spec());
        Assert.Equal(HitTarget.Body, frame.HitTest(1, 1, out _));
        Assert.Equal(HitTarget.Device, frame.HitTest(5, 5, out var device));
        Assert.Equal("Mouse", device!.Name);
        Assert.Equal(HitTarget.None, frame.HitTest(15, 3, out _));
        Assert.Equal(HitTarget.None, frame.HitTest(-1, 50, out _));
    }

    [Fact]
    public void Battery_bar_is_colored_and_clickable()
    {
        var frame = FrameComposer.Compose(Layout, Sprites, new[] { Mouse(80) }, Spec());
        Assert.Equal(BatteryBar.ColorFor(80), Pixel(frame, 0, 8));
        Assert.Equal(HitTarget.Device, frame.HitTest(0, 8, out _));
    }

    [Fact]
    public void Body_dy_moves_body_and_following_places()
    {
        var frame = FrameComposer.Compose(Layout, Sprites, new[] { Mouse() }, Spec(dy: 1));
        Assert.Equal(0u, Pixel(frame, 0, 0));
        Assert.Equal(Red, Pixel(frame, 0, 2));
        Assert.Equal(Blue, Pixel(frame, 5, 6));
    }

    [Fact]
    public void Critical_device_blinks_but_keeps_its_bar()
    {
        var frame = FrameComposer.Compose(Layout, Sprites, new[] { Mouse(5) }, Spec(criticalVisible: false));
        Assert.Equal(0u, Pixel(frame, 5, 5));
        Assert.Equal(HitTarget.Device, frame.HitTest(0, 8, out _));
    }

    [Fact]
    public void Seat_is_drawn_under_the_body()
    {
        var keyboard = new DevicePlacement(TestReadings.Make("Keys", DeviceKind.Keyboard), "seat", "keyboard");
        var frame = FrameComposer.Compose(Layout, Sprites, new[] { keyboard }, Spec());
        Assert.Equal(HitTarget.Body, frame.HitTest(1, 1, out _));
        Assert.Equal(HitTarget.Device, frame.HitTest(2, 1, out _));
    }

    [Fact]
    public void Overlays_are_drawn_and_clipped()
    {
        var frame = FrameComposer.Compose(Layout, Sprites, Array.Empty<DevicePlacement>(),
            Spec(0, true, new Overlay("fx", 10, 3), new Overlay("fx", 99, 99)));
        Assert.Equal(Blue, Pixel(frame, 10, 3));
    }

    [Theory]
    [InlineData(100, 4)]
    [InlineData(76, 4)]
    [InlineData(75, 3)]
    [InlineData(26, 2)]
    [InlineData(25, 1)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void Filled_segments(int percent, int expected) =>
        Assert.Equal(expected, BatteryBar.FilledSegments(percent));

    [Fact]
    public void Unknown_battery_fills_grey() =>
        Assert.Equal(BatteryBar.SegmentCount, BatteryBar.FilledSegments(null));

    [Theory]
    [InlineData(51, BatteryBar.Green)]
    [InlineData(50, BatteryBar.Amber)]
    [InlineData(21, BatteryBar.Amber)]
    [InlineData(20, BatteryBar.Red)]
    public void Bar_colors_follow_thresholds(int percent, uint expected) =>
        Assert.Equal(expected, BatteryBar.ColorFor(percent));
}
