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
        ["bolt"] = new("bolt", 1, 1, new[] { Yellow }),
        ["full"] = new("full", 1, 1, new[] { Green }),
    };

    const uint Yellow = 0xFFFFFF00, Green = 0xFF00FF00;

    static ComposedFrame Compose(IReadOnlyList<DevicePlacement> placements, FrameSpec spec, BatteryStyle style = BatteryStyle.Bar) =>
        FrameComposer.Compose(Layout, Sprites, placements, spec, style);

    static FrameSpec Spec(int dy = 0, bool criticalVisible = true, params Overlay[] overlays) =>
        new("body", dy, overlays, criticalVisible);

    static DevicePlacement Mouse(int battery = 80) =>
        new(TestReadings.Make("Mouse", DeviceKind.Mouse, battery: battery), "hands", "mouse");

    static uint Pixel(ComposedFrame f, int x, int y) => f.Pixels[y * f.Width + x];

    [Fact]
    public void Hit_test_distinguishes_body_device_and_empty()
    {
        var frame = Compose(new[] { Mouse() }, Spec());
        Assert.Equal(HitTarget.Body, frame.HitTest(1, 1, out _));
        Assert.Equal(HitTarget.Device, frame.HitTest(5, 5, out var device));
        Assert.Equal("Mouse", device!.Name);
        Assert.Equal(HitTarget.None, frame.HitTest(15, 3, out _));
        Assert.Equal(HitTarget.None, frame.HitTest(-1, 50, out _));
    }

    [Fact]
    public void Battery_bar_is_colored_and_clickable()
    {
        var frame = Compose(new[] { Mouse(80) }, Spec());
        Assert.Equal(BatteryBar.ColorFor(80), Pixel(frame, 0, 8));
        Assert.Equal(HitTarget.Device, frame.HitTest(0, 8, out _));
    }

    [Fact]
    public void Body_dy_moves_body_and_following_places()
    {
        var frame = Compose(new[] { Mouse() }, Spec(dy: 1));
        Assert.Equal(0u, Pixel(frame, 0, 0));
        Assert.Equal(Red, Pixel(frame, 0, 2));
        Assert.Equal(Blue, Pixel(frame, 5, 6));
    }

    [Fact]
    public void Critical_device_blinks_but_keeps_its_bar()
    {
        var frame = Compose(new[] { Mouse(5) }, Spec(criticalVisible: false));
        Assert.Equal(0u, Pixel(frame, 5, 5));
        Assert.Equal(HitTarget.Device, frame.HitTest(0, 8, out _));
    }

    [Fact]
    public void Seat_is_drawn_under_the_body()
    {
        var keyboard = new DevicePlacement(TestReadings.Make("Keys", DeviceKind.Keyboard), "seat", "keyboard");
        var frame = Compose(new[] { keyboard }, Spec());
        Assert.Equal(HitTarget.Body, frame.HitTest(1, 1, out _));
        Assert.Equal(HitTarget.Device, frame.HitTest(2, 1, out _));
    }

    [Fact]
    public void Overlays_are_drawn_and_clipped()
    {
        var frame = Compose(Array.Empty<DevicePlacement>(),
            Spec(0, true, new Overlay("fx", 10, 3), new Overlay("fx", 99, 99)));
        Assert.Equal(Blue, Pixel(frame, 10, 3));
    }

    [Fact]
    public void Outline_style_rings_the_device_in_its_battery_color_and_drops_the_bar()
    {
        var frame = Compose(new[] { Mouse(30) }, Spec(), BatteryStyle.Outline);
        uint amber = BatteryBar.ColorFor(30);
        Assert.Equal(new[] { amber, amber, amber, amber },
            new[] { Pixel(frame, 4, 5), Pixel(frame, 6, 5), Pixel(frame, 5, 4), Pixel(frame, 5, 6) });
        Assert.Equal(Blue, Pixel(frame, 5, 5));
        Assert.Equal(HitTarget.Device, frame.HitTest(6, 5, out _));
        Assert.Equal(0u, Pixel(frame, 0, 8));
    }

    [Fact]
    public void Outline_stays_while_a_critical_device_blinks()
    {
        var frame = Compose(new[] { Mouse(5) }, Spec(criticalVisible: false), BatteryStyle.Outline);
        Assert.Equal(BatteryBar.Red, Pixel(frame, 4, 5));
    }

    [Fact]
    public void Charging_device_gets_a_bolt_right_of_its_sprite()
    {
        var charging = Mouse() with { Device = Mouse().Device with { IsCharging = true } };
        var frame = Compose(new[] { charging }, Spec(), BatteryStyle.Outline);
        Assert.Equal(Yellow, Pixel(frame, 7, 3));
        Assert.Equal(HitTarget.Device, frame.HitTest(7, 3, out _));
    }

    [Fact]
    public void Bolt_flips_left_at_the_canvas_edge()
    {
        var edge = new SkinLayout(20, 12, new PixelPoint(0, 0),
            new Dictionary<string, Place> { ["hands"] = new(new[] { new PixelPoint(19, 5) }, new PixelPoint(0, 8), false) },
            new Dictionary<string, PixelPoint>());
        var charging = Mouse() with { Device = Mouse().Device with { IsCharging = true } };
        var frame = FrameComposer.Compose(edge, Sprites, new[] { charging }, Spec(), BatteryStyle.Outline);
        Assert.Equal(Yellow, Pixel(frame, 16, 3));
    }

    [Fact]
    public void Full_device_gets_the_full_icon_instead_of_a_bolt()
    {
        var full = Mouse(100) with { Device = Mouse(100).Device with { IsCharging = true } };
        var frame = Compose(new[] { full }, Spec(), BatteryStyle.Outline);
        Assert.Equal(Green, Pixel(frame, 7, 3));
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
