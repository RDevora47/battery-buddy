using BatteryBuddy.Pet.Animation;
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
    public void Ships_every_pet() =>
        Assert.Equal(new[] { "axolotl", "bunny", "choppa", "missy", "panda", "parrot", "redpanda" }, TestSkin.Names);

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
        if (Layout.Perched)
        {
            Assert.Null(Layout.MousePaw);   // standing on the keyboard, its feet stay there
            return;
        }
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

    [Fact]
    public void Only_mango_is_perched() =>
        Assert.Equal(new[] { "parrot" }, TestSkin.Names.Where(n => TestSkin.Load(n).Layout.Perched));

    [Fact]
    public void Mango_stands_on_the_keyboard_and_hops_within_the_canvas()
    {
        var (_, Layout, Sprites) = TestSkin.Load("parrot");
        var body = Sprites["body_idle"];
        var keyboard = Layout.Places["seat"].Points[0];
        Assert.True(Layout.Body.Y + body.ArtHeight <= keyboard.Y, "the body stays above the keys; the feet stand on them");
        Assert.InRange(Layout.Body.X * 2 + body.Width / 2, keyboard.X * 2 + Sprites["keyboard"].Width / 2 - 1,
            keyboard.X * 2 + Sprites["keyboard"].Width / 2 + 1);   // centred on the keyboard, in half art pixels
        foreach (var dx in new[] { -1, 1 })
            foreach (var sprite in new[] { "body_idle", "gills_perky" })
                AssertFits(Layout, $"{sprite} mid-hop", Layout.Body.X + dx, Layout.Body.Y - PetAnimator.HopHeight,
                    Sprites[sprite].ArtWidth, Sprites[sprite].ArtHeight);
    }

    [Theory, MemberData(nameof(Skins))]
    public void Every_sprite_is_drawn_at_2x(string skin) =>
        Assert.All(TestSkin.Load(skin).Sprites.Values, s => Assert.True(s.Density == 2, $"sprite {s.Name} is @{s.Density}x"));

    [Theory]
    [InlineData("axolotl", 0)]   // body_idle art row of the top of the head
    [InlineData("choppa", 7)]    // rows 0-6 are for the ears
    [InlineData("missy", 3)]
    [InlineData("redpanda", 3)]
    [InlineData("bunny", 9)]     // rows 0-8 are for the long ears
    [InlineData("panda", 4)]
    [InlineData("parrot", 5)]     // small and perched on the keyboard
    public void Saiyan_hair_flames_up_high_above_the_head(string skin, int headTop)
    {
        // Classic Super Saiyan: the spikes rise above the head by more than the axolotl's whole head is tall
        // (10 rows; Choppa's long head is taller than the canvas leaves room for).
        const int risesAbove = 10;
        var (_, Layout, Sprites) = TestSkin.Load(skin);
        var hair = Layout.Overlays["saiyan"];
        int top = Layout.Body.Y + headTop;
        Assert.True(top - hair.Y > risesAbove, $"hair top {hair.Y} is only {top - hair.Y} rows above the head");
        Assert.True(hair.Y + Sprites["saiyan"].ArtHeight > top, "hair must reach down onto the head");
    }

    public static readonly string[] TailSprites = { "tail", "tail_wag1", "tail_wag2" };

    [Theory]
    [InlineData("choppa")]
    [InlineData("missy")]
    [InlineData("redpanda")]
    public void Tailed_pets_have_a_tail_that_wags_within_the_canvas(string skin)
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

    [Theory]
    [InlineData("choppa")]
    [InlineData("missy")]
    [InlineData("redpanda")]
    public void Every_tail_pose_stays_attached_to_the_body(string skin)
    {
        // The tail is drawn behind the body: every pixel that shows must connect, through other showing
        // tail pixels, to the body's edge, in every pose of the wag (both bob positions look the same).
        var (_, Layout, Sprites) = TestSkin.Load(skin);
        var body = Sprites["body_idle"];
        var tailAt = Layout.Tail!.Value;
        foreach (var name in TailSprites)
        {
            var tail = Sprites[name];
            int d = tail.Density;
            bool Body(int x, int y)
            {
                int bx = x - Layout.Body.X * d, by = y - Layout.Body.Y * d;
                return bx >= 0 && by >= 0 && bx < body.Width && by < body.Height && body.Pixels[by * body.Width + bx] != 0;
            }
            bool Shows(int x, int y)
            {
                int tx = x - tailAt.X * d, ty = y - tailAt.Y * d;
                return tx >= 0 && ty >= 0 && tx < tail.Width && ty < tail.Height && tail.Pixels[ty * tail.Width + tx] != 0 && !Body(x, y);
            }
            var showing = new List<(int X, int Y)>();
            for (int ty = 0; ty < tail.Height; ty++)
                for (int tx = 0; tx < tail.Width; tx++)
                    if (Shows(tailAt.X * d + tx, tailAt.Y * d + ty)) showing.Add((tailAt.X * d + tx, tailAt.Y * d + ty));
            var steps = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            var reached = new HashSet<(int, int)>(showing.Where(p => steps.Any(s => Body(p.X + s.Item1, p.Y + s.Item2))));
            var queue = new Queue<(int X, int Y)>(reached);
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (dx, dy) in steps)
                    if (Shows(x + dx, y + dy) && reached.Add((x + dx, y + dy))) queue.Enqueue((x + dx, y + dy));
            }
            var loose = showing.Where(p => !reached.Contains(p)).ToList();
            Assert.True(loose.Count == 0, $"{name}: {loose.Count} tail pixels float free of the body, e.g. ({loose.FirstOrDefault().X / 2.0}, {loose.FirstOrDefault().Y / 2.0})");
        }
    }

    [Theory]
    [InlineData("choppa")]
    [InlineData("missy")]
    [InlineData("redpanda")]
    public void Wagging_swings_the_tail_about_a_base_tucked_behind_the_body(string skin)
    {
        // The bottom rows (the base) are the same in every pose, and all of them sit behind the body.
        const int baseRows = 4;
        var (_, Layout, Sprites) = TestSkin.Load(skin);
        var rest = Sprites["tail"];
        int from = (rest.ArtHeight - baseRows) * rest.Density * rest.Width;
        foreach (var name in TailSprites)
            Assert.True(Sprites[name].Pixels.Skip(from).SequenceEqual(rest.Pixels.Skip(from)), $"{name} moves the tail's base");

        var body = Sprites["body_idle"];
        var tail = Layout.Tail!.Value;
        int d = rest.Density, hidden = 0, total = 0;
        for (int y = (rest.ArtHeight - baseRows) * d; y < rest.Height; y++)
            for (int x = 0; x < rest.Width; x++)
            {
                if (rest.Pixels[y * rest.Width + x] == 0) continue;
                total++;
                int bx = tail.X * d + x - Layout.Body.X * d, by = tail.Y * d + y - Layout.Body.Y * d;
                if (bx >= 0 && by >= 0 && bx < body.Width && by < body.Height && body.Pixels[by * body.Width + bx] != 0) hidden++;
            }
        Assert.True(hidden * 2 >= total, $"only {hidden} of the base's {total} pixels are tucked behind the body");
    }

    [Theory]
    [InlineData("choppa")]
    [InlineData("missy")]
    [InlineData("redpanda")]
    public void Tails_are_open_curls_not_rings(string skin)
    {
        // A see-through hole enclosed by the tail itself reads as a loose ring once it swings clear of the body.
        var sprites = TestSkin.Load(skin).Sprites;
        foreach (var name in TailSprites)
        {
            var t = sprites[name];
            var outside = new HashSet<(int, int)>();
            var queue = new Queue<(int X, int Y)>();
            void Visit(int x, int y)
            {
                if (x < -1 || y < -1 || x > t.Width || y > t.Height) return;
                bool opaque = x >= 0 && y >= 0 && x < t.Width && y < t.Height && t.Pixels[y * t.Width + x] != 0;
                if (!opaque && outside.Add((x, y))) queue.Enqueue((x, y));
            }
            Visit(-1, -1);
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                Visit(x + 1, y); Visit(x - 1, y); Visit(x, y + 1); Visit(x, y - 1);
            }
            int holes = 0;
            for (int y = 0; y < t.Height; y++)
                for (int x = 0; x < t.Width; x++)
                    if (t.Pixels[y * t.Width + x] == 0 && !outside.Contains((x, y))) holes++;
            Assert.True(holes == 0, $"{name} encloses {holes} see-through pixels");
        }
    }

    // Choppa's body_idle, in art rows: the head runs from the crown to the chin; her shoulders are below it.
    const int ChoppaCrown = 7, ChoppaChin = 19, ChoppaShoulders = 22;

    // First and last opaque sprite pixel columns over a range of art rows (@2x sprite rows).
    static (int Left, int Right) Extent(Sprite s, int fromRow, int toRow)
    {
        int left = int.MaxValue, right = int.MinValue;
        for (int y = fromRow * s.Density; y < Math.Min((toRow + 1) * s.Density, s.Height); y++)
            for (int x = 0; x < s.Width; x++)
                if (s.Pixels[y * s.Width + x] != 0) { left = Math.Min(left, x); right = Math.Max(right, x); }
        return (left, right);
    }

    [Fact]
    public void Choppas_shoulders_are_as_wide_as_her_head()
    {
        var body = TestSkin.Load("choppa").Sprites["body_idle"];
        var head = Extent(body, ChoppaCrown, ChoppaChin);
        var shoulders = Extent(body, ChoppaShoulders, body.ArtHeight - 1);
        Assert.Equal(head, shoulders);
    }

    [Fact]
    public void Choppas_ears_stay_within_the_sides_of_her_head()
    {
        var sprites = TestSkin.Load("choppa").Sprites;
        var head = Extent(sprites["body_idle"], ChoppaCrown, ChoppaChin);
        foreach (var ears in new[] { "gills_perky", "gills_droopy", "gills_limp" })
        {
            var e = Extent(sprites[ears], 0, sprites[ears].ArtHeight - 1);
            Assert.True(e.Left >= head.Left && e.Right <= head.Right,
                $"{ears} spans columns {e.Left}-{e.Right}, the head only {head.Left}-{head.Right}");
        }
    }

    [Fact]
    public void Choppas_fluffy_cheeks_are_15_percent_bigger()
    {
        // Her silhouette over the cheek rows (below the eyes, above the chin: art rows 14-18) grew by 15 %
        // from the 320 half-pixels of the first slim redraw.
        const int before = 320;
        var body = TestSkin.Load("choppa").Sprites["body_idle"];
        int area = 0;
        for (int y = 14 * body.Density; y < 19 * body.Density; y++)
            for (int x = 0; x < body.Width; x++)
                if (body.Pixels[y * body.Width + x] != 0) area++;
        Assert.Equal((int)Math.Ceiling(before * 1.15), area);
    }

    [Fact]
    public void Choppas_upright_ears_are_eleven_rows_long()
    {
        // 20 % longer than the first long ears' 9 rows, rounded up; they reach down to where they meet the crown.
        var perky = TestSkin.Load("choppa").Sprites["gills_perky"];
        int top = Enumerable.Range(0, perky.Height).First(y => Enumerable.Range(0, perky.Width).Any(x => perky.Pixels[y * perky.Width + x] != 0));
        int bottom = Enumerable.Range(0, perky.Height).Last(y => Enumerable.Range(0, perky.Width).Any(x => perky.Pixels[y * perky.Width + x] != 0));
        Assert.Equal(11, (bottom - top + 1) / perky.Density);
    }

    [Theory]
    [InlineData("axolotl")]
    [InlineData("bunny")]   // the cottontail hides behind the body
    [InlineData("panda")]
    [InlineData("parrot")]
    public void Some_pets_have_no_tail(string skin) => Assert.Null(TestSkin.Load(skin).Layout.Tail);

    [Theory]
    [InlineData("choppa")]
    [InlineData("missy")]
    [InlineData("redpanda")]
    [InlineData("bunny")]
    [InlineData("panda")]
    [InlineData("parrot")]
    public void Ears_and_crests_sink_as_the_battery_drains(string skin)
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
    [InlineData("choppa", 7, 12)]
    [InlineData("missy", 3, 8)]
    [InlineData("redpanda", 3, 8)]
    [InlineData("bunny", 9, 14)]
    [InlineData("panda", 4, 11)]
    [InlineData("parrot", 5, 9)]
    public void Straw_hat_sits_on_the_head_above_the_eyes(string skin, int headTop, int eyeRow)
    {
        var (_, Layout, Sprites) = TestSkin.Load(skin);
        var hat = Layout.Overlays["strawhat"];
        int top = Layout.Body.Y + headTop, bottom = hat.Y + Sprites["strawhat"].ArtHeight;
        Assert.InRange(bottom, top + 1, Layout.Body.Y + eyeRow - 1);   // brim on the head, above the eyes (body art rows)
        int centre = hat.X * 2 + Sprites["strawhat"].Width / 2, body = Layout.Body.X * 2 + Sprites["body_idle"].Width / 2;
        Assert.InRange(centre, body - 1, body + 1);   // centred on the pet, in half art pixels
    }

    [Theory, MemberData(nameof(Skins))]
    public void The_scanning_magnifier_stays_on_the_canvas_and_off_the_earbuds(string skin)
    {
        // It swings up to 3 right and 1 up from its spot (PetAnimator's sweep); the pet doesn't bob while scanning.
        var (_, Layout, Sprites) = TestSkin.Load(skin);
        var glass = Sprites["magnifier"];
        var earbud = Sprites["earbud"];
        var at = Layout.Overlays["magnifier"];
        for (int dx = 0; dx <= 3; dx++)
            for (int dy = -1; dy <= 0; dy++)
            {
                AssertFits(Layout, "magnifier", at.X + dx, at.Y + dy, glass.ArtWidth, glass.ArtHeight);
                foreach (var bud in Layout.Places["gills"].Points)
                    for (int py = bud.Y * 2; py < (bud.Y + earbud.ArtHeight) * 2; py++)
                        for (int px = bud.X * 2; px < (bud.X + earbud.ArtWidth) * 2; px++)
                        {
                            int gx = px - (at.X + dx) * 2, gy = py - (at.Y + dy) * 2;
                            bool covered = gx >= 0 && gy >= 0 && gx < glass.Width && gy < glass.Height && glass.Pixels[gy * glass.Width + gx] != 0;
                            Assert.False(covered, $"the magnifier covers the earbud at ({bud.X}, {bud.Y})");
                        }
            }
    }

    static void AssertFits(SkinLayout Layout, string what, int x, int y, int w, int h) =>
        Assert.True(x >= 0 && y >= 0 && x + w <= Layout.CanvasWidth && y + h <= Layout.CanvasHeight,
            $"{what} at ({x},{y}) size {w}x{h} exceeds canvas {Layout.CanvasWidth}x{Layout.CanvasHeight}");
}
