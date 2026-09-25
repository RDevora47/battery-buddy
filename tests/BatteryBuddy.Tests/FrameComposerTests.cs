using BatteryBuddy.Devices;
using BatteryBuddy.Pet.Scene;

namespace BatteryBuddy.Tests;

public class FrameComposerTests
{
    const uint Red = 0xFFFF0000, Blue = 0xFF0000FF;

    // 20x12 canvas; 2x2 body at (0,0); a 1x1 "mouse" at hands (5,5) that follows the body;
    // a 3x1 "keyboard" seat at (0,1) over the body.
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

    static FrameSpec Spec(int dy = 0, int criticalDx = 0, params Overlay[] overlays) =>
        new("body", dy, overlays, criticalDx);

    static DevicePlacement Mouse(int battery = 80) =>
        new(TestReadings.Make("Mouse", DeviceKind.Mouse, battery: battery), "hands", "mouse");

    static uint Pixel(ComposedFrame f, int x, int y) => f.Pixels[y * f.Width + x];

    [Fact]
    public void Tail_is_drawn_behind_the_body_and_bobs_with_it()
    {
        // 2x1 tail at (1,0): its left pixel is under the body's right column.
        var layout = Layout with { Tail = new PixelPoint(1, 0) };
        var sprites = new Dictionary<string, Sprite>(Sprites) { ["tail_wag1"] = new("tail_wag1", 2, 1, new[] { Blue, Blue }) };
        var frame = FrameComposer.Compose(layout, sprites, Array.Empty<DevicePlacement>(), Spec(dy: 1) with { Tail = "tail_wag1" });
        Assert.Equal(Red, Pixel(frame, 1, 1));    // body wins
        Assert.Equal(Blue, Pixel(frame, 2, 1));   // tail sticks out, one row down with the bob
        Assert.Equal(0u, Pixel(frame, 2, 0));
        Assert.Equal(HitTarget.Body, frame.HitTest(2, 1, out _));
    }

    [Fact]
    public void A_skin_without_a_tail_ignores_the_wag() =>
        Assert.Equal(4, Compose(Array.Empty<DevicePlacement>(), Spec() with { Tail = "tail_wag1" }).Pixels.Count(p => p != 0));

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
    public void A_stale_battery_is_drawn_dimmed()
    {
        var stale = new DevicePlacement(TestReadings.Make("Mouse", DeviceKind.Mouse, battery: 80) with { BatteryStale = true }, "hands", "mouse");
        var frame = Compose(new[] { stale }, Spec());
        Assert.Equal(BatteryBar.ColorFor(80, stale: true), Pixel(frame, 0, 8));
        Assert.NotEqual(BatteryBar.ColorFor(80), BatteryBar.ColorFor(80, stale: true));
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
    public void Critical_device_shakes_but_its_bar_stays()
    {
        var frame = Compose(new[] { Mouse(5) }, Spec(criticalDx: 1));
        Assert.Equal(0u, Pixel(frame, 5, 5));
        Assert.Equal(Blue, Pixel(frame, 6, 5));
        Assert.Equal(BatteryBar.ColorFor(5), Pixel(frame, 0, 8));
    }

    [Fact]
    public void Healthy_device_ignores_the_shake()
    {
        var frame = Compose(new[] { Mouse(80) }, Spec(criticalDx: 1));
        Assert.Equal(Blue, Pixel(frame, 5, 5));
    }

    [Fact]
    public void Gills_draw_over_the_body_and_fade_greys_both_but_not_devices()
    {
        var frame = Compose(new[] { Mouse() }, new FrameSpec("body", 0, Array.Empty<Overlay>(), Gills: "fx", Fade: 1));
        // Luminance grey: red -> 0.3 * 255 = 76.5 (rounds to even, 0x4C); the blue gill -> 0.11 * 255 = 28 (0x1C).
        Assert.Equal(0xFF4C4C4Cu, Pixel(frame, 1, 1));
        Assert.Equal(0xFF1C1C1Cu, Pixel(frame, 0, 0));
        Assert.Equal(Blue, Pixel(frame, 5, 5));
    }

    [Fact]
    public void Desk_sits_in_front_of_the_body_with_the_keyboard_on_it_under_the_paws()
    {
        // 4x1 desk at (0,1) over the 2x2 body; the 3x1 keyboard on it; a paw at (0,1) over the keyboard.
        var layout = Layout with
        {
            Desk = new PixelPoint(0, 1),
            Paws = new Dictionary<Paw, PixelPoint> { [Paw.Left] = new(0, 1), [Paw.Right] = new(9, 9) },
        };
        var sprites = new Dictionary<string, Sprite>(Sprites)
        {
            ["desk"] = new("desk", 4, 1, new[] { Yellow, Yellow, Yellow, Yellow }),
            ["paw"] = new("paw", 1, 1, new[] { Green }),
        };
        var keyboard = new DevicePlacement(TestReadings.Make("Keys", DeviceKind.Keyboard), "seat", "keyboard");
        var frame = FrameComposer.Compose(layout, sprites, new[] { keyboard }, Spec(dy: 1), BatteryStyle.Bar);

        Assert.Equal(Green, Pixel(frame, 0, 2));    // paw bobs with the body, over the keyboard
        Assert.Equal(Blue, Pixel(frame, 1, 1));     // keyboard over the desk and body
        Assert.Equal(Yellow, Pixel(frame, 3, 1));   // desk over the body, and it doesn't bob
        Assert.Equal(Red, Pixel(frame, 1, 2));      // body shows below the desk here only because the test desk is 1 row tall
        Assert.Equal(HitTarget.Body, frame.HitTest(3, 1, out _));
    }

    [Fact]
    public void A_perched_pet_stands_in_front_of_the_desk_and_hops_with_its_feet_and_devices()
    {
        // The same desk and keyboard, but the 2x2 body stands in front of them; a foot at (0,3); the hands'
        // device at (5,5) follows the body.
        var layout = Layout with
        {
            Desk = new PixelPoint(0, 1),
            Paws = new Dictionary<Paw, PixelPoint> { [Paw.Left] = new(0, 3), [Paw.Right] = new(15, 3) },
            MousePaw = new PixelPoint(12, 3),
            Perched = true,
        };
        var sprites = new Dictionary<string, Sprite>(Sprites)
        {
            ["desk"] = new("desk", 4, 1, new[] { Yellow, Yellow, Yellow, Yellow }),
            ["paw"] = new("paw", 1, 1, new[] { Green }),
        };
        var keyboard = new DevicePlacement(TestReadings.Make("Keys", DeviceKind.Keyboard), "seat", "keyboard");

        var rest = FrameComposer.Compose(layout, sprites, new[] { keyboard, Mouse() }, Spec(), BatteryStyle.Bar);
        Assert.Equal(Red, Pixel(rest, 1, 1));      // the body covers the keyboard and desk
        Assert.Equal(Yellow, Pixel(rest, 3, 1));

        var hop = FrameComposer.Compose(layout, sprites, new[] { keyboard, Mouse() },
            Spec(dy: -1) with { BodyDx = 1, PawLeftDy = 1, RightPawOnMouse = true }, BatteryStyle.Bar);
        Assert.Equal(Red, Pixel(hop, 1, 0));       // up one, right one
        Assert.Equal(Blue, Pixel(hop, 0, 1));      // the keyboard shows where the body was
        Assert.Equal(Green, Pixel(hop, 1, 2));     // the foot hops along instead of pressing down
        Assert.Equal(Green, Pixel(hop, 16, 2));    // and neither foot goes to the mouse
        Assert.Equal(0u, Pixel(hop, 12, 3));
        Assert.Equal(Blue, Pixel(hop, 6, 4));      // a device the pet holds hops with it
    }

    [Fact]
    public void Overlays_are_drawn_and_clipped()
    {
        var frame = Compose(Array.Empty<DevicePlacement>(),
            Spec(0, 0, new Overlay("fx", 10, 3), new Overlay("fx", 99, 99)));
        Assert.Equal(Blue, Pixel(frame, 10, 3));
    }

    [Fact]
    public void Outline_style_rings_the_device_in_its_battery_color_and_drops_the_bar()
    {
        var frame = Compose(new[] { Mouse(80) }, Spec(), BatteryStyle.Outline);
        uint green = BatteryBar.ColorFor(80);
        Assert.Equal(new[] { green, green, green, green },
            new[] { Pixel(frame, 4, 5), Pixel(frame, 6, 5), Pixel(frame, 5, 4), Pixel(frame, 5, 6) });
        Assert.Equal(Blue, Pixel(frame, 5, 5));
        Assert.Equal(HitTarget.Device, frame.HitTest(6, 5, out _));
        Assert.Equal(0u, Pixel(frame, 0, 8));
    }

    [Theory]
    [InlineData(BatteryStyle.Outline)]
    [InlineData(BatteryStyle.Bar)]
    [InlineData(BatteryStyle.Gauge)]
    public void A_device_without_a_battery_is_drawn_bare(BatteryStyle style)
    {
        var bare = new DevicePlacement(TestReadings.Make("Mouse", DeviceKind.Mouse, battery: null) with { NoBattery = true }, "hands", "mouse");
        var frame = Compose(new[] { bare }, Spec(), style);
        Assert.Equal(Blue, Pixel(frame, 5, 5));
        Assert.Equal(HitTarget.Device, frame.HitTest(5, 5, out _));
        Assert.Equal(4 + 1, frame.Pixels.Count(p => p != 0));   // the body and the mouse, nothing else
    }

    [Fact]
    public void Outline_covers_only_the_remaining_charge_clockwise_from_the_top()
    {
        // The 1x1 mouse's ring is 4 pixels: top, right, bottom, left. 30% keeps ceil(1.2) = 2 of them.
        var frame = Compose(new[] { Mouse(30) }, Spec(), BatteryStyle.Outline);
        uint amber = BatteryBar.ColorFor(30);
        Assert.Equal(new[] { amber, amber, 0u, 0u },
            new[] { Pixel(frame, 5, 4), Pixel(frame, 6, 5), Pixel(frame, 5, 6), Pixel(frame, 4, 5) });
    }

    [Fact]
    public void Outline_drains_anticlockwise_leaving_the_top_right_quarter_at_25_percent()
    {
        // A 4x4 pad at (5,5): its 16-pixel ring runs x 4..9, y 4..9 (no corners).
        var sprites = new Dictionary<string, Sprite>(Sprites) { ["pad"] = new("pad", 4, 4, Enumerable.Repeat(Blue, 16).ToArray()) };
        var pad = new DevicePlacement(TestReadings.Make("Pad", DeviceKind.Mouse, battery: 25), "hands", "pad");
        var frame = FrameComposer.Compose(Layout, sprites, new[] { pad }, Spec(), BatteryStyle.Outline);
        uint color = BatteryBar.ColorFor(25);
        // Kept: the right half of the top edge and the top half of the right edge.
        Assert.All(new[] { (7, 4), (8, 4), (9, 5), (9, 6) }, p => Assert.Equal(color, Pixel(frame, p.Item1, p.Item2)));
        // Drained: the top-left, the lower right edge, the bottom and the left.
        Assert.All(new[] { (5, 4), (6, 4), (9, 7), (9, 8), (7, 9), (4, 6) }, p => Assert.Equal(0u, Pixel(frame, p.Item1, p.Item2)));
    }

    [Fact]
    public void Outline_stays_while_a_critical_device_blinks()
    {
        // 5% keeps just the ring's top pixel, above the shaken mouse.
        var frame = Compose(new[] { Mouse(5) }, Spec(criticalDx: 1), BatteryStyle.Outline);
        Assert.Equal(BatteryBar.Red, Pixel(frame, 6, 4));
        Assert.Equal(Blue, Pixel(frame, 6, 5));
    }

    [Fact]
    public void Charging_device_gets_a_bolt_at_its_top_right()
    {
        var charging = Mouse() with { Device = Mouse().Device with { IsCharging = true } };
        var frame = Compose(new[] { charging }, Spec(), BatteryStyle.Outline);
        // Above the 1x1 mouse at (5,5), aligned to its right edge.
        Assert.Equal(Yellow, Pixel(frame, 5, 4));
        Assert.Equal(HitTarget.Device, frame.HitTest(5, 4, out _));
    }

    [Fact]
    public void Status_icon_stays_within_the_canvas_at_the_edge()
    {
        var edge = new SkinLayout(20, 12, new PixelPoint(0, 0),
            new Dictionary<string, Place> { ["hands"] = new(new[] { new PixelPoint(19, 5) }, new PixelPoint(0, 8), false) },
            new Dictionary<string, PixelPoint>());
        var charging = Mouse() with { Device = Mouse().Device with { IsCharging = true } };
        var frame = FrameComposer.Compose(edge, Sprites, new[] { charging }, Spec(), BatteryStyle.Outline);
        Assert.Equal(Yellow, Pixel(frame, 19, 4));   // clamped to the last column, not off-canvas
    }

    [Fact]
    public void Full_device_gets_the_full_icon_instead_of_a_bolt()
    {
        var full = Mouse(100) with { Device = Mouse(100).Device with { IsCharging = true } };
        var frame = Compose(new[] { full }, Spec(), BatteryStyle.Outline);
        Assert.Equal(Green, Pixel(frame, 5, 4));
    }

    [Fact]
    public void A_2x_sprite_doubles_the_frame_and_keeps_its_art_footprint()
    {
        var mixed = new Dictionary<string, Sprite>(Sprites)
        {
            ["bolt"] = new("bolt", 2, 2, new[] { Yellow, 0u, 0u, Green }, Density: 2),
        };
        var charging = Mouse() with { Device = Mouse().Device with { IsCharging = true } };
        // Bar style, so no outline ring shows through the bolt's transparent pixels.
        var frame = FrameComposer.Compose(Layout, mixed, new[] { charging }, Spec(), BatteryStyle.Bar);

        Assert.Equal((2, 40, 24), (frame.Resolution, frame.Width, frame.Height));
        // 1x art fills 2x2 blocks: the body pixel at art (1,1) and the mouse at art (5,5).
        Assert.All(new[] { (2, 2), (3, 3) }, p => Assert.Equal(Red, Pixel(frame, p.Item1, p.Item2)));
        Assert.Equal(Blue, Pixel(frame, 11, 11));
        // The bolt sits at art (5,4) — above the mouse — drawn with half-size pixels (frame x 10..11, y 8..9).
        Assert.Equal(new[] { Yellow, 0u, 0u, Green },
            new[] { Pixel(frame, 10, 8), Pixel(frame, 11, 8), Pixel(frame, 10, 9), Pixel(frame, 11, 9) });
        Assert.Equal(HitTarget.Device, frame.HitTest(11, 9, out _));
    }

    // Mini battery tests use a skin with one @2x sprite, so the frame is 2x and mixels are single pixels.
    static ComposedFrame ComposeGauge(DevicePlacement placement)
    {
        var mixed = new Dictionary<string, Sprite>(Sprites) { ["bolt"] = new("bolt", 2, 2, new[] { Yellow, Yellow, Yellow, Yellow }, Density: 2) };
        return FrameComposer.Compose(Layout, mixed, new[] { placement }, Spec(), BatteryStyle.Gauge);
    }

    // Lit cells of the gauge right of the 1x1 mouse at art (5,5): art x 7 = frame x 14, centred on frame rows 10..11.
    static int GaugeLitCells(ComposedFrame frame)
    {
        int top = 5 * 2 + (2 - BatteryGauge.MixelHeight) / 2;
        return Enumerable.Range(1, BatteryGauge.CellCount)
            .Count(cell => Pixel(frame, 15, top + BatteryGauge.MixelHeight - 1 - cell) != BatteryBar.Empty);
    }

    [Fact]
    public void Gauge_sits_right_of_the_device_with_its_cells_lit_and_no_ring_or_bar()
    {
        var frame = ComposeGauge(Mouse(55));
        Assert.Equal(3, GaugeLitCells(frame));
        int top = 10 + (2 - BatteryGauge.MixelHeight) / 2;
        Assert.Equal(BatteryGauge.Shell, Pixel(frame, 14, top + 1));
        Assert.Equal(BatteryBar.Green, Pixel(frame, 15, top + 6));
        Assert.Equal(HitTarget.Device, frame.HitTest(15, top + 6, out _));
        Assert.Equal(0u, Pixel(frame, 8, 10));   // no outline ring left of the mouse
        Assert.Equal(0u, Pixel(frame, 0, 16));   // no bar
    }

    [Fact]
    public void Charging_gauge_keeps_its_level_and_gets_a_bolt_beside_it()
    {
        var charging = Mouse(30) with { Device = Mouse(30).Device with { IsCharging = true } };
        var frame = ComposeGauge(charging);
        Assert.Equal(2, GaugeLitCells(frame));
        // Gauge covers art x 7..8; the 1x1-art bolt sits past a 1px gap at art (10,5) = frame (20..21, 10..11).
        Assert.Equal(Yellow, Pixel(frame, 20, 10));
        Assert.Equal(HitTarget.Device, frame.HitTest(21, 11, out _));
        Assert.Equal(0u, Pixel(ComposeGauge(Mouse(30)), 20, 10));
    }

    [Fact]
    public void Full_gauge_shows_no_status_icon()
    {
        var full = Mouse(100) with { Device = Mouse(100).Device with { IsCharging = true } };
        Assert.Equal(0u, Pixel(ComposeGauge(full), 20, 10));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(20, 1)]
    [InlineData(21, 2)]
    [InlineData(80, 4)]
    [InlineData(81, 5)]
    [InlineData(null, 5)]
    public void Gauge_lit_cells(int? percent, int expected) =>
        Assert.Equal(expected, BatteryGauge.LitCells(percent));

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

    [Fact]
    public void Clicking_drops_the_right_paw_and_the_mouse_but_typing_leaves_the_mouse()
    {
        // 1x1 paws at (0,4) and (3,4) on the body; a 1x1 mouse at (4,4) held in the right paw.
        var layout = Layout with
        {
            Places = new Dictionary<string, Place>
            {
                ["righthand"] = new(new[] { new PixelPoint(4, 4) }, new PixelPoint(0, 8), true, FollowsClick: true),
            },
            Paws = new Dictionary<Paw, PixelPoint> { [Paw.Left] = new(0, 4), [Paw.Right] = new(3, 4) },
        };
        var sprites = new Dictionary<string, Sprite>(Sprites) { ["paw"] = new("paw", 1, 1, new[] { Green }) };
        var mouse = new DevicePlacement(TestReadings.Make("Mouse", DeviceKind.Mouse, battery: 80), "righthand", "mouse");

        var rest = FrameComposer.Compose(layout, sprites, new[] { mouse }, Spec(), BatteryStyle.Bar);
        Assert.Equal(Green, Pixel(rest, 0, 4));
        Assert.Equal(Green, Pixel(rest, 3, 4));
        Assert.Equal(Blue, Pixel(rest, 4, 4));

        var typing = FrameComposer.Compose(layout, sprites, new[] { mouse }, Spec() with { PawRightDy = 1 }, BatteryStyle.Bar);
        Assert.Equal(Green, Pixel(typing, 3, 5));
        Assert.Equal(Blue, Pixel(typing, 4, 4));

        var click = FrameComposer.Compose(layout, sprites, new[] { mouse }, Spec() with { PawRightDy = 1, ClickDy = 1 }, BatteryStyle.Bar);
        Assert.Equal(Green, Pixel(click, 0, 4));   // left paw stays
        Assert.Equal(0u, Pixel(click, 3, 4));
        Assert.Equal(Green, Pixel(click, 3, 5));
        Assert.Equal(Blue, Pixel(click, 4, 5));
        Assert.Equal(HitTarget.Body, click.HitTest(3, 5, out _));
    }

    [Fact]
    public void Right_paw_reaches_for_the_mouse_over_it_and_ignores_the_bob()
    {
        // Paws rest at (0,4) and (3,4); on the mouse the right paw sits at (8,3), over a 1x2 mouse at (8,3).
        var layout = Layout with
        {
            Places = new Dictionary<string, Place> { ["righthand"] = new(new[] { new PixelPoint(8, 3) }, new PixelPoint(0, 8), false, FollowsClick: true) },
            Paws = new Dictionary<Paw, PixelPoint> { [Paw.Left] = new(0, 4), [Paw.Right] = new(3, 4) },
            MousePaw = new PixelPoint(8, 3),
        };
        var sprites = new Dictionary<string, Sprite>(Sprites)
        {
            ["paw"] = new("paw", 1, 1, new[] { Green }),
            ["tallmouse"] = new("tallmouse", 1, 2, new[] { Blue, Blue }),
        };
        var mouse = new DevicePlacement(TestReadings.Make("Mouse", DeviceKind.Mouse, battery: 80), "righthand", "tallmouse");

        var reach = FrameComposer.Compose(layout, sprites, new[] { mouse }, Spec(dy: 1) with { RightPawOnMouse = true }, BatteryStyle.Bar);
        Assert.Equal(0u, Pixel(reach, 3, 5));        // gone from the body
        Assert.Equal(Green, Pixel(reach, 0, 5));     // the left paw still bobs
        Assert.Equal(Green, Pixel(reach, 8, 3));     // over the mouse, not bobbing
        Assert.Equal(Blue, Pixel(reach, 8, 4));

        var press = FrameComposer.Compose(layout, sprites, new[] { mouse },
            Spec() with { RightPawOnMouse = true, PawRightDy = 1, ClickDy = 1 }, BatteryStyle.Bar);
        Assert.Equal(Green, Pixel(press, 8, 4));
        Assert.Equal(Blue, Pixel(press, 8, 5));
    }

    [Fact]
    public void A_reaching_limb_clicks_the_mouse_while_the_right_paw_keeps_its_place()
    {
        // As above, plus a 1x2 limb at (8,1) reaching down to the paw on the mouse (Boba's long tentacle), a
        // 1x1 frame of it on the way there, and a tail at (6,0) whose "tail_reach" pose shows while it's out.
        var layout = Layout with
        {
            Places = new Dictionary<string, Place> { ["righthand"] = new(new[] { new PixelPoint(8, 3) }, new PixelPoint(0, 8), false, FollowsClick: true) },
            Paws = new Dictionary<Paw, PixelPoint> { [Paw.Left] = new(0, 4), [Paw.Right] = new(3, 4) },
            MousePaw = new PixelPoint(8, 3),
            Reach = new PixelPoint(8, 1),
            Tail = new PixelPoint(6, 0),
        };
        var sprites = new Dictionary<string, Sprite>(Sprites)
        {
            ["paw"] = new("paw", 1, 1, new[] { Green }),
            ["reach"] = new("reach", 1, 2, new[] { Yellow, Yellow }),
            ["reach1"] = new("reach1", 1, 1, new[] { Yellow }),
            ["tail"] = new("tail", 1, 1, new[] { Red }),
            ["tail_reach"] = new("tail_reach", 1, 1, new[] { Yellow }),
            ["tallmouse"] = new("tallmouse", 1, 2, new[] { Blue, Blue }),
        };
        var mouse = new DevicePlacement(TestReadings.Make("Mouse", DeviceKind.Mouse, battery: 80), "righthand", "tallmouse");

        var rest = FrameComposer.Compose(layout, sprites, new[] { mouse }, Spec() with { Tail = "tail" }, BatteryStyle.Bar);
        Assert.Equal(Red, Pixel(rest, 6, 0));
        Assert.Equal(0u, Pixel(rest, 8, 1));

        var reach = FrameComposer.Compose(layout, sprites, new[] { mouse }, Spec(dy: 1) with { RightPawOnMouse = true, Tail = "tail" }, BatteryStyle.Bar);
        Assert.Equal(Green, Pixel(reach, 3, 5));     // the right paw stays on the body, bobbing with it
        Assert.Equal(Yellow, Pixel(reach, 6, 1));    // the tail's reaching pose, bobbing
        Assert.Equal(Yellow, Pixel(reach, 8, 1));    // the limb, not bobbing
        Assert.Equal(Yellow, Pixel(reach, 8, 2));
        Assert.Equal(Green, Pixel(reach, 8, 3));     // its paw on the mouse
        Assert.Equal(HitTarget.Body, reach.HitTest(8, 1, out _));

        var press = FrameComposer.Compose(layout, sprites, new[] { mouse },
            Spec() with { RightPawOnMouse = true, PawRightDy = 1, ClickDy = 1, Tail = "tail" }, BatteryStyle.Bar);
        Assert.Equal(Green, Pixel(press, 3, 4));     // the click doesn't press the paw on the keys
        Assert.Equal(0u, Pixel(press, 3, 5));
        Assert.Equal(Yellow, Pixel(press, 8, 1));    // the limb stays put
        Assert.Equal(Yellow, Pixel(press, 8, 2));
        Assert.Equal(Green, Pixel(press, 8, 4));     // its paw presses the button
        Assert.Equal(Blue, Pixel(press, 8, 5));

        var onTheWay = FrameComposer.Compose(layout, sprites, new[] { mouse },
            Spec() with { RightPawOnMouse = true, Tail = "tail", ReachStep = 1 }, BatteryStyle.Bar);
        Assert.Equal(Yellow, Pixel(onTheWay, 8, 1)); // its first frame
        Assert.Equal(0u, Pixel(onTheWay, 8, 2));
        Assert.Equal(Blue, Pixel(onTheWay, 8, 3));   // no paw on the mouse yet
    }

    // A 3x2 device at (8,4) whose icons go to the given side; a 1x2 bolt (or a 1x1 @2x one) and 1x1 full icon.
    static ComposedFrame ComposeIcons(Side side, DeviceReading device, BatteryStyle style, bool hiRes = false)
    {
        var layout = Layout with
        {
            Places = new Dictionary<string, Place> { ["seat"] = new(new[] { new PixelPoint(8, 4) }, new PixelPoint(0, 10), false, Icons: side) },
        };
        var sprites = new Dictionary<string, Sprite>(Sprites)
        {
            ["wide"] = new("wide", 3, 2, new[] { Blue, Blue, Blue, Blue, Blue, Blue }),
            ["bolt"] = hiRes ? new("bolt", 2, 2, new[] { Yellow, Yellow, Yellow, Yellow }, Density: 2) : new("bolt", 1, 2, new[] { Yellow, Yellow }),
        };
        return FrameComposer.Compose(layout, sprites, new[] { new DevicePlacement(device, "seat", "wide") }, Spec(), style);
    }

    static DeviceReading Keys(int battery, bool charging = false) =>
        TestReadings.Make("Keys", DeviceKind.Keyboard, battery: battery) with { IsCharging = charging };

    [Theory]
    [InlineData(BatteryStyle.Bar, 6)]       // 1px gap
    [InlineData(BatteryStyle.Outline, 5)]   // 2px gap, clear of the ring
    public void Icons_left_centres_the_status_icon_left_of_the_device(BatteryStyle style, int x)
    {
        var frame = ComposeIcons(Side.Left, Keys(60, charging: true), style);
        Assert.Equal(Yellow, Pixel(frame, x, 4));
        Assert.Equal(Yellow, Pixel(frame, x, 5));
        Assert.Equal(0u, Pixel(frame, 10, 2));   // no badge above the device (row 3 may hold the ring)
        Assert.Equal(HitTarget.Device, frame.HitTest(x, 4, out _));
    }

    [Fact]
    public void Icons_right_centres_the_status_icon_right_of_the_device()
    {
        var frame = ComposeIcons(Side.Right, Keys(100), BatteryStyle.Bar);
        // A 1x1 icon can't centre on 2 rows at 1x; it takes the upper one.
        Assert.Equal(Green, Pixel(frame, 12, 4));
    }

    [Fact]
    public void Icons_left_puts_the_mini_battery_left_with_its_bolt_beyond_it()
    {
        // 2x frame: the device spans frame x 16..21, rows 8..11.
        var frame = ComposeIcons(Side.Left, Keys(60, charging: true), BatteryStyle.Gauge, hiRes: true);
        int gaugeX = (8 - 1 - BatteryGauge.ArtWidth) * 2;
        Assert.Contains(Enumerable.Range(0, 20), y => Pixel(frame, gaugeX + 1, y) != 0);
        int boltX = gaugeX - 2 * 2;                  // 1 art px gap, 1 art px bolt
        Assert.Equal(Yellow, Pixel(frame, boltX, 9));
        Assert.All(Enumerable.Range(0, 20), y => Assert.Equal(0u, Pixel(frame, 23, y)));   // nothing on the right
    }
}
