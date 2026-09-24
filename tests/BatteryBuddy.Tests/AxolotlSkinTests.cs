using BatteryBuddy.Pet.Scene;

namespace BatteryBuddy.Tests;

public class AxolotlSkinTests
{
    static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "Skins", "axolotl");
    static readonly IReadOnlyDictionary<string, Sprite> Sprites = SpriteSheetParser.Parse(File.ReadAllText(Path.Combine(Dir, "sprites.txt")));
    static readonly SkinLayout Layout = SkinLayout.Parse(File.ReadAllText(Path.Combine(Dir, "skin.json")));

    public static readonly string[] RequiredSprites =
    {
        "body_idle", "body_blink", "body_sleepy", "body_worried", "body_sniff_l", "body_sniff_r",
        "earbud", "mouse", "gamepad", "keyboard", "phone", "gadget",
        "smoke1", "smoke2", "smoke3", "ploof_text", "zzz", "sweat",
        "bolt", "full", "whoosh1", "whoosh2", "whoosh3", "saiyan",
        "gills_perky", "gills_droopy", "gills_limp", "paw", "desk",
    };

    // The sprite(s) each place is designed to hold.
    static readonly Dictionary<string, string[]> PlaceSprites = new()
    {
        ["gills"] = new[] { "earbud" }, ["neck"] = new[] { "earbud" },
        ["hands"] = new[] { "gamepad" }, ["righthand"] = new[] { "mouse" },
        ["seat"] = new[] { "keyboard" }, ["side"] = new[] { "phone" },
        ["float1"] = new[] { "gadget" }, ["float2"] = new[] { "gadget" },
    };

    [Fact]
    public void Has_all_required_sprites() =>
        Assert.All(RequiredSprites, name => Assert.True(Sprites.ContainsKey(name), $"missing sprite {name}"));

    [Fact]
    public void Body_frames_share_one_size()
    {
        var sizes = RequiredSprites.Where(n => n.StartsWith("body_")).Select(n => (Sprites[n].Width, Sprites[n].Height)).Distinct();
        Assert.Single(sizes);
    }

    [Fact]
    public void Has_all_places_and_overlays()
    {
        Assert.All(SkinLayout.RequiredPlaces, p => Assert.True(Layout.Places.ContainsKey(p), $"missing place {p}"));
        Assert.True(Layout.Overlays.ContainsKey("zzz"));
        Assert.True(Layout.Overlays.ContainsKey("sweat"));
        Assert.True(Layout.Overlays.ContainsKey("saiyan"));
        Assert.NotNull(Layout.Paws);
        Assert.Equal(new[] { Paw.Left, Paw.Right }, Layout.Paws!.Keys.Order());
    }

    [Fact]
    public void Mouse_lies_on_the_desk_beside_the_keyboard_and_only_moves_on_clicks()
    {
        var mouse = Layout.Places["righthand"];
        Assert.True(mouse.FollowsClick);
        Assert.False(mouse.FollowsBody);   // idle bobs move the pet, not the desk

        var desk = Layout.Desk!.Value;
        var at = mouse.Points[0];
        var keyboard = Layout.Places["seat"].Points[0];
        Assert.True(at.X > keyboard.X + Sprites["keyboard"].ArtWidth, "mouse must be right of the keyboard, outline included");
        Assert.True(at.Y >= desk.Y && at.X + Sprites["mouse"].ArtWidth < desk.X + Sprites["desk"].ArtWidth &&
            at.Y + Sprites["mouse"].ArtHeight + 1 <= desk.Y + Sprites["desk"].ArtHeight, "mouse must lie on the desk, clicks included");
    }

    [Fact]
    public void Right_paw_reaches_the_mouse_buttons()
    {
        var paw = Layout.MousePaw!.Value;
        var mouse = Layout.Places["righthand"].Points[0];
        Assert.InRange(paw.X, mouse.X, mouse.X + Sprites["mouse"].ArtWidth - Sprites["paw"].ArtWidth);
        Assert.Equal(mouse.Y, paw.Y + Sprites["paw"].ArtHeight - 1);   // bottom row rests on the mouse's top edge
    }

    [Fact]
    public void Keyboard_icons_go_left_and_mouse_icons_go_right()
    {
        Assert.Equal(Side.Left, Layout.Places["seat"].Icons);
        Assert.Equal(Side.Right, Layout.Places["righthand"].Icons);
        // The widest thing beside a device: the mini battery and its bolt (1px gaps), or a status icon
        // clear of the outline ring (2px gap).
        int bolt = Sprites["bolt"].ArtWidth;
        int beside = Math.Max(1 + BatteryGauge.ArtWidth + 1 + bolt, 2 + Math.Max(bolt, Sprites["full"].ArtWidth));
        var keyboard = Layout.Places["seat"].Points[0];
        var mouse = Layout.Places["righthand"].Points[0];
        Assert.True(keyboard.X - beside >= 0, "keyboard icons fit left");
        Assert.True(mouse.X + Sprites["mouse"].ArtWidth + beside <= Layout.CanvasWidth, "mouse icons fit right");
    }

    [Fact]
    public void Keyboard_lies_on_the_desk_under_both_paws()
    {
        var desk = Sprites["desk"];
        var keyboard = Sprites["keyboard"];
        var at = Layout.Places["seat"].Points[0];
        Assert.NotNull(Layout.Desk);
        var d = Layout.Desk!.Value;
        Assert.True(at.X >= d.X && at.Y >= d.Y && at.X + keyboard.ArtWidth <= d.X + desk.ArtWidth && at.Y + keyboard.ArtHeight <= d.Y + desk.ArtHeight,
            "keyboard must lie within the desk");
        foreach (var paw in Layout.Paws!.Values)
        {
            int x = paw.X + Sprites["paw"].ArtWidth / 2;   // the paw's middle column is over the keys
            Assert.InRange(x, at.X, at.X + keyboard.ArtWidth - 1);
            Assert.Equal(at.Y, paw.Y + Sprites["paw"].ArtHeight);   // resting just above the keys, a press lands on them
        }
    }

    [Fact]
    public void Everything_fits_the_canvas_including_bob()
    {
        var body = Sprites["body_idle"];
        AssertFits("body", Layout.Body.X, Layout.Body.Y + 1, body.ArtWidth, body.ArtHeight);
        foreach (var gills in RequiredSprites.Where(n => n.StartsWith("gills_")))
            AssertFits(gills, Layout.Body.X, Layout.Body.Y + 1, Sprites[gills].ArtWidth, Sprites[gills].ArtHeight);
        AssertFits("desk", Layout.Desk!.Value.X, Layout.Desk.Value.Y, Sprites["desk"].ArtWidth, Sprites["desk"].ArtHeight);
        foreach (var (paw, pt) in Layout.Paws!)
            AssertFits($"paw {paw}", pt.X, pt.Y + 2, Sprites["paw"].ArtWidth, Sprites["paw"].ArtHeight);   // bob + press
        foreach (var (name, place) in Layout.Places)
        {
            int dy = (place.FollowsBody ? 1 : 0) + (place.FollowsClick ? 1 : 0);
            foreach (var spriteName in PlaceSprites[name])
                foreach (var pt in place.Points)
                    AssertFits($"{name}/{spriteName}", pt.X, pt.Y + dy, Sprites[spriteName].ArtWidth, Sprites[spriteName].ArtHeight);
            AssertFits($"{name}/bar", place.Bar.X, place.Bar.Y + dy, BatteryBar.Width, BatteryBar.Height);
        }
        foreach (var (name, pt) in Layout.Overlays)   // overlays bob with the pet
            AssertFits($"overlay {name}", pt.X, pt.Y + 1, Sprites[name].ArtWidth, Sprites[name].ArtHeight);
    }

    [Fact]
    public void Every_sprite_is_drawn_at_2x() =>
        Assert.All(Sprites.Values, s => Assert.True(s.Density == 2, $"sprite {s.Name} is @{s.Density}x"));

    [Fact]
    public void Saiyan_hair_flames_up_higher_than_the_head_is_tall()
    {
        // Classic Super Saiyan: the spikes rise above the head by more than the head's own height.
        const int headRows = 10;   // body_idle rows 0-9 in art pixels: the head down to the chin
        var hair = Layout.Overlays["saiyan"];
        Assert.True(Layout.Body.Y - hair.Y > headRows, $"hair top {hair.Y} is only {Layout.Body.Y - hair.Y} rows above the head");
        Assert.True(hair.Y + Sprites["saiyan"].ArtHeight > Layout.Body.Y, "hair must reach down onto the head");
    }

    [Fact]
    public void Saiyan_hair_leaves_the_earbuds_and_their_full_badges_visible()
    {
        // The hair shows when every device is nearly full, so the gills' earbuds may wear a "full" badge above them.
        var hair = Sprites["saiyan"];
        var at = Layout.Overlays["saiyan"];   // bobs with the pet, like the gills' earbuds
        var earbud = Sprites["earbud"];
        var full = Sprites["full"];
        foreach (var pt in Layout.Places["gills"].Points)
        {
            var clear = new[]
            {
                (pt.X, pt.Y, earbud.ArtWidth, earbud.ArtHeight),
                (pt.X + earbud.ArtWidth - full.ArtWidth, pt.Y - full.ArtHeight, full.ArtWidth, full.ArtHeight),
            };
            foreach (var (x, y, w, h) in clear)
                for (int py = y * hair.Density; py < (y + h) * hair.Density; py++)
                    for (int px = x * hair.Density; px < (x + w) * hair.Density; px++)
                    {
                        int hx = px - at.X * hair.Density, hy = py - at.Y * hair.Density;
                        bool covered = hx >= 0 && hy >= 0 && hx < hair.Width && hy < hair.Height && hair.Pixels[hy * hair.Width + hx] != 0;
                        Assert.False(covered, $"hair covers ({px / 2.0}, {py / 2.0}) near the earbud at ({pt.X}, {pt.Y})");
                    }
        }
    }

    static void AssertFits(string what, int x, int y, int w, int h) =>
        Assert.True(x >= 0 && y >= 0 && x + w <= Layout.CanvasWidth && y + h <= Layout.CanvasHeight,
            $"{what} at ({x},{y}) size {w}x{h} exceeds canvas {Layout.CanvasWidth}x{Layout.CanvasHeight}");
}
