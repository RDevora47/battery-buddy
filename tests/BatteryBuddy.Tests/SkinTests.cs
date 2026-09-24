using BatteryBuddy.Pet.Scene;

namespace BatteryBuddy.Tests;

/// <summary>A pet: its layout and its sprites drawn over the shared sheet, as the app loads them.</summary>
public sealed record TestSkin(string Name, SkinLayout Layout, IReadOnlyDictionary<string, Sprite> Sprites)
{
    public static readonly string Root = Path.Combine(AppContext.BaseDirectory, "Skins");

    /// <summary>Every pet folder (all but the shared "common" one).</summary>
    public static IEnumerable<string> Names => Directory.GetDirectories(Root).Select(Path.GetFileName)
        .Where(n => n != "common").Order()!;

    public static TestSkin Load(string name) => new(name,
        SkinLayout.Parse(File.ReadAllText(Path.Combine(Root, name, "skin.json"))),
        SpriteSheetParser.Parse(File.ReadAllText(Path.Combine(Root, "common", "sprites.txt")),
            File.ReadAllText(Path.Combine(Root, name, "sprites.txt"))));

    public override string ToString() => Name;
}

public class SkinTests
{
    public static TheoryData<string> Skins
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in TestSkin.Names) data.Add(name);
            return data;
        }
    }

    public static readonly string[] RequiredSprites =
    {
        "body_idle", "body_blink", "body_sleepy", "body_worried", "body_sniff_l", "body_sniff_r",
        "earbud", "mouse", "gamepad", "keyboard", "phone", "gadget",
        "smoke1", "smoke2", "smoke3", "ploof_text", "zzz", "sweat",
        "bolt", "full", "whoosh1", "whoosh2", "whoosh3", "saiyan", "strawhat",
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
    public void Ships_the_axolotl_and_the_dogs() =>
        Assert.Equal(new[] { "axolotl", "choppa", "missy" }, TestSkin.Names);

    [Theory, MemberData(nameof(Skins))]
    public void Has_all_required_sprites(string skin)
    {
        var (_, _, Sprites) = TestSkin.Load(skin);
        Assert.All(RequiredSprites, name => Assert.True(Sprites.ContainsKey(name), $"missing sprite {name}"));
    }

    [Theory, MemberData(nameof(Skins))]
    public void Body_frames_share_one_size(string skin)
    {
        var (_, _, Sprites) = TestSkin.Load(skin);
        var sizes = RequiredSprites.Where(n => n.StartsWith("body_")).Select(n => (Sprites[n].Width, Sprites[n].Height)).Distinct();
        Assert.Single(sizes);
    }

    [Theory, MemberData(nameof(Skins))]
    public void Has_all_places_and_overlays(string skin)
    {
        var (_, Layout, Sprites) = TestSkin.Load(skin);
        Assert.All(SkinLayout.RequiredPlaces, p => Assert.True(Layout.Places.ContainsKey(p), $"missing place {p}"));
        Assert.True(Layout.Overlays.ContainsKey("zzz"));
        Assert.True(Layout.Overlays.ContainsKey("sweat"));
        Assert.True(Layout.Overlays.ContainsKey("saiyan"));
        Assert.True(Layout.Overlays.ContainsKey("strawhat"));
        Assert.NotNull(Layout.Paws);
        Assert.Equal(new[] { Paw.Left, Paw.Right }, Layout.Paws!.Keys.Order());
    }

    [Theory, MemberData(nameof(Skins))]
    public void Mouse_lies_on_the_desk_beside_the_keyboard_and_only_moves_on_clicks(string skin)
    {
        var (_, Layout, Sprites) = TestSkin.Load(skin);
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

    [Theory, MemberData(nameof(Skins))]
    public void Right_paw_reaches_the_mouse_buttons(string skin)
    {
        var (_, Layout, Sprites) = TestSkin.Load(skin);
        var paw = Layout.MousePaw!.Value;
        var mouse = Layout.Places["righthand"].Points[0];
        Assert.InRange(paw.X, mouse.X, mouse.X + Sprites["mouse"].ArtWidth - Sprites["paw"].ArtWidth);
        Assert.Equal(mouse.Y, paw.Y + Sprites["paw"].ArtHeight - 1);   // bottom row rests on the mouse's top edge
    }

    [Theory, MemberData(nameof(Skins))]
    public void Keyboard_icons_go_left_and_mouse_icons_go_right(string skin)
    {
        var (_, Layout, Sprites) = TestSkin.Load(skin);
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

    [Theory, MemberData(nameof(Skins))]
    public void Keyboard_lies_on_the_desk_under_both_paws(string skin)
    {
        var (_, Layout, Sprites) = TestSkin.Load(skin);
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

    [Theory, MemberData(nameof(Skins))]
    public void Everything_fits_the_canvas_including_bob(string skin)
    {
        var (_, Layout, Sprites) = TestSkin.Load(skin);
        var body = Sprites["body_idle"];
        AssertFits(Layout, "body", Layout.Body.X, Layout.Body.Y + 1, body.ArtWidth, body.ArtHeight);
        foreach (var gills in RequiredSprites.Where(n => n.StartsWith("gills_")))
            AssertFits(Layout, gills, Layout.Body.X, Layout.Body.Y + 1, Sprites[gills].ArtWidth, Sprites[gills].ArtHeight);
        AssertFits(Layout, "desk", Layout.Desk!.Value.X, Layout.Desk.Value.Y, Sprites["desk"].ArtWidth, Sprites["desk"].ArtHeight);
        foreach (var (paw, pt) in Layout.Paws!)
            AssertFits(Layout, $"paw {paw}", pt.X, pt.Y + 2, Sprites["paw"].ArtWidth, Sprites["paw"].ArtHeight);   // bob + press
        foreach (var (name, place) in Layout.Places)
        {
            int dy = (place.FollowsBody ? 1 : 0) + (place.FollowsClick ? 1 : 0);
            foreach (var spriteName in PlaceSprites[name])
                foreach (var pt in place.Points)
                    AssertFits(Layout, $"{name}/{spriteName}", pt.X, pt.Y + dy, Sprites[spriteName].ArtWidth, Sprites[spriteName].ArtHeight);
            AssertFits(Layout, $"{name}/bar", place.Bar.X, place.Bar.Y + dy, BatteryBar.Width, BatteryBar.Height);
        }
        foreach (var (name, pt) in Layout.Overlays)   // overlays bob with the pet
            AssertFits(Layout, $"overlay {name}", pt.X, pt.Y + 1, Sprites[name].ArtWidth, Sprites[name].ArtHeight);
    }

    [Theory, MemberData(nameof(Skins))]
    public void Every_sprite_is_drawn_at_2x(string skin) =>
        Assert.All(TestSkin.Load(skin).Sprites.Values, s => Assert.True(s.Density == 2, $"sprite {s.Name} is @{s.Density}x"));

    [Theory]
    [InlineData("axolotl", 0, 10)]   // body_idle art rows 0-9: the head down to the chin
    [InlineData("choppa", 6, 12)]    // three rows of headroom for the tall ears; crown at row 6, chin at row 17
    [InlineData("missy", 3, 12)]     // curls from row 3, beard down to row 14
    public void Saiyan_hair_flames_up_higher_than_the_head_is_tall(string skin, int headTop, int headRows)
    {
        // Classic Super Saiyan: the spikes rise above the head by more than the head's own height.
        var (_, Layout, Sprites) = TestSkin.Load(skin);
        var hair = Layout.Overlays["saiyan"];
        int top = Layout.Body.Y + headTop;
        Assert.True(top - hair.Y > headRows, $"hair top {hair.Y} is only {top - hair.Y} rows above the head");
        Assert.True(hair.Y + Sprites["saiyan"].ArtHeight > top, "hair must reach down onto the head");
    }

    public static readonly string[] TailSprites = { "tail", "tail_wag1", "tail_wag2" };

    [Theory]
    [InlineData("choppa")]
    [InlineData("missy")]
    public void Dogs_have_a_tail_that_wags_within_the_canvas(string skin)
    {
        var (_, Layout, Sprites) = TestSkin.Load(skin);
        Assert.NotNull(Layout.Tail);
        var tail = Layout.Tail!.Value;
        Assert.All(TailSprites, name => Assert.True(Sprites.ContainsKey(name), $"missing sprite {name}"));
        Assert.Single(TailSprites.Select(n => (Sprites[n].Width, Sprites[n].Height)).Distinct());   // swapped in place
        Assert.Equal(3, TailSprites.Select(n => string.Join(",", Sprites[n].Pixels)).Distinct().Count());   // each pose differs
        foreach (var name in TailSprites)
            AssertFits(Layout, name, tail.X, tail.Y + 1, Sprites[name].ArtWidth, Sprites[name].ArtHeight);   // bobs with the pet
    }

    [Fact]
    public void The_axolotl_has_no_tail() => Assert.Null(TestSkin.Load("axolotl").Layout.Tail);

    [Theory]
    [InlineData("choppa")]
    [InlineData("missy")]
    public void Dog_ears_sink_as_the_battery_drains(string skin)
    {
        // The ears fill the "gills" slot: each mood is its own pose, and the lower the battery, the lower the ears sit.
        var sprites = TestSkin.Load(skin).Sprites;
        int Top(string ears)
        {
            var s = sprites[ears];
            return Enumerable.Range(0, s.Height).First(y => Enumerable.Range(0, s.Width).Any(x => s.Pixels[y * s.Width + x] != 0));
        }
        Assert.True(Top("gills_perky") < Top("gills_droopy"), "droopy ears must sit lower than perky ones");
        Assert.True(Top("gills_droopy") < Top("gills_limp"), "limp ears must sit lower than droopy ones");
    }

    /// <summary>Every skin crossed with every full-charge hat's sprite.</summary>
    public static TheoryData<string, string> SkinsAndHats
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var name in TestSkin.Names)
                foreach (var hat in new[] { "saiyan", "strawhat" }) data.Add(name, hat);
            return data;
        }
    }

    [Theory, MemberData(nameof(SkinsAndHats))]
    public void Full_charge_hats_leave_the_earbuds_and_their_full_badges_visible(string skin, string hatSprite)
    {
        var (_, Layout, Sprites) = TestSkin.Load(skin);
        // A hat shows when every device is nearly full, so the gills' earbuds may wear a "full" badge above them.
        var hair = Sprites[hatSprite];
        var at = Layout.Overlays[hatSprite];   // bobs with the pet, like the gills' earbuds
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
                        Assert.False(covered, $"{hatSprite} covers ({px / 2.0}, {py / 2.0}) near the earbud at ({pt.X}, {pt.Y})");
                    }
        }
    }

    [Theory]
    [InlineData("axolotl", 0, 8)]
    [InlineData("choppa", 6, 11)]
    [InlineData("missy", 3, 8)]
    public void Straw_hat_sits_on_the_head_above_the_eyes(string skin, int headTop, int eyeRow)
    {
        var (_, Layout, Sprites) = TestSkin.Load(skin);
        var hat = Layout.Overlays["strawhat"];
        int top = Layout.Body.Y + headTop, bottom = hat.Y + Sprites["strawhat"].ArtHeight;
        Assert.InRange(bottom, top + 1, Layout.Body.Y + eyeRow - 1);   // brim on the head, above the eyes (body art rows)
        int centre = hat.X * 2 + Sprites["strawhat"].Width / 2, body = Layout.Body.X * 2 + Sprites["body_idle"].Width / 2;
        Assert.InRange(centre, body - 1, body + 1);   // centred on the pet, in half art pixels
    }

    static void AssertFits(SkinLayout Layout, string what, int x, int y, int w, int h) =>
        Assert.True(x >= 0 && y >= 0 && x + w <= Layout.CanvasWidth && y + h <= Layout.CanvasHeight,
            $"{what} at ({x},{y}) size {w}x{h} exceeds canvas {Layout.CanvasWidth}x{Layout.CanvasHeight}");
}
