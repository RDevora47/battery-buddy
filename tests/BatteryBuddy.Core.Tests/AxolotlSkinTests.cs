using BatteryBuddy.Core.Scene;

namespace BatteryBuddy.Core.Tests;

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
    }

    [Fact]
    public void Everything_fits_the_canvas_including_bob()
    {
        var body = Sprites["body_idle"];
        AssertFits("body", Layout.Body.X, Layout.Body.Y + 1, body.Width, body.Height);
        foreach (var (name, place) in Layout.Places)
        {
            int dy = place.FollowsBody ? 1 : 0;
            foreach (var spriteName in PlaceSprites[name])
                foreach (var pt in place.Points)
                    AssertFits($"{name}/{spriteName}", pt.X, pt.Y + dy, Sprites[spriteName].Width, Sprites[spriteName].Height);
            AssertFits($"{name}/bar", place.Bar.X, place.Bar.Y + dy, BatteryBar.Width, BatteryBar.Height);
        }
    }

    static void AssertFits(string what, int x, int y, int w, int h) =>
        Assert.True(x >= 0 && y >= 0 && x + w <= Layout.CanvasWidth && y + h <= Layout.CanvasHeight,
            $"{what} at ({x},{y}) size {w}x{h} exceeds canvas {Layout.CanvasWidth}x{Layout.CanvasHeight}");
}
