using System.Text.Json;

namespace BatteryBuddy.Pet.Scene;

public readonly record struct PixelPoint(int X, int Y);

public enum Paw { Left, Right }

public enum Side { Left, Right }

/// <summary>
/// Where a device goes: sprite drawn at each point, battery bar at Bar. FollowsBody = bobs with the pet;
/// FollowsClick = held in the clicking paw, so it dips on mouse clicks (but not while that paw types).
/// Icons = the side the status icon and mini battery go on, vertically centred; null = badge at the top-right.
/// </summary>
public sealed record Place(IReadOnlyList<PixelPoint> Points, PixelPoint Bar, bool FollowsBody, bool FollowsClick = false, Side? Icons = null);

public sealed record SkinLayout(
    int CanvasWidth,
    int CanvasHeight,
    PixelPoint Body,
    IReadOnlyDictionary<string, Place> Places,
    IReadOnlyDictionary<string, PixelPoint> Overlays,
    IReadOnlyDictionary<Paw, PixelPoint>? Paws = null,
    PixelPoint? Desk = null,
    PixelPoint? MousePaw = null,
    PixelPoint? Tail = null,
    bool Perched = false,
    PixelPoint? Reach = null,
    bool Taps = false,
    int PawLift = 0)
{
    // Paws: the "paw" sprite drawn over the body at these points. Desk: the "desk" sprite in front of the
    // body, which the seat's device lies on. MousePaw: where the right paw goes to click the mouse.
    // Tail: the tail sprites ("tail", "tail_wag1", "tail_wag2"), drawn behind the body and bobbing with it.
    // All are canvas positions like Body. Perched: a small pet standing on the keyboard instead of sitting
    // behind the desk; it's drawn in front of the desk, its paws are its feet, and it hops as it types
    // (Mango) - or, with Taps, taps the keys with its feet like paws (Gumersindo, too heavy to hop about).
    // Reach: where the "reach" sprite goes, a whole limb running to MousePaw (Boba's long tentacle, out from
    // under the desk). A skin with one clicks with that limb, the paw drawn at its tip, and its right paw keeps
    // typing. On the way there and back it shows "reach1", "reach2"..., and its tail pose is "tail_reach".
    // PawLift: how many rows above Paws a sitting pet rests its paws (Crispin's hooves on his belly); a tap
    // still comes down to Paws and onto the keys.
    /// <summary>A perched pet that hops from key to key as it types, rather than tapping them.</summary>
    public bool Hops => Perched && !Taps;

    public static readonly string[] RequiredPlaces = { "gills", "neck", "hands", "righthand", "seat", "side", "float1", "float2" };

    /// <summary>
    /// Where devices without a place of their own go, in order: "float1", "float2" from skin.json, then as many
    /// more as fit on the canvas, stacked on at the same step (see <see cref="Parse"/>).
    /// </summary>
    public IReadOnlyList<string> FloatPlaces => Places.Keys
        .Where(name => name.StartsWith(FloatPrefix) && int.TryParse(name[FloatPrefix.Length..], out _))
        .OrderBy(name => int.Parse(name[FloatPrefix.Length..]))
        .ToList();

    const string FloatPrefix = "float";

    /// <summary>
    /// Canvas width plus room on the right for the channel bubbles over the mouse (transparent columns, so the
    /// pet doesn't move). The bitmap is this wide; CanvasWidth stays the skin's own for icon placement.
    /// </summary>
    public int FrameWidth => Places.TryGetValue("righthand", out var hand)
        ? Math.Max(CanvasWidth, ChannelArc.RequiredWidth(hand.Points[0]))
        : CanvasWidth;

    public static SkinLayout Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var canvas = root.GetProperty("canvas");

        var places = new Dictionary<string, Place>();
        foreach (var p in root.GetProperty("places").EnumerateObject())
        {
            places[p.Name] = new Place(
                p.Value.GetProperty("points").EnumerateArray().Select(Point).ToList(),
                Point(p.Value.GetProperty("bar")),
                p.Value.GetProperty("followsBody").GetBoolean(),
                p.Value.TryGetProperty("followsClick", out var click) && click.GetBoolean(),
                p.Value.TryGetProperty("icons", out var icons) ? Enum.Parse<Side>(icons.GetString()!, ignoreCase: true) : null);
        }

        StackFloats(places);
        var overlays = root.GetProperty("overlays").EnumerateObject().ToDictionary(o => o.Name, o => Point(o.Value));

        return new SkinLayout(
            canvas.GetProperty("width").GetInt32(),
            canvas.GetProperty("height").GetInt32(),
            Point(root.GetProperty("body")),
            places,
            overlays,
            root.TryGetProperty("paws", out var paws)
                ? paws.EnumerateObject().ToDictionary(p => Enum.Parse<Paw>(p.Name, ignoreCase: true), p => Point(p.Value))
                : null,
            root.TryGetProperty("desk", out var desk) ? Point(desk) : null,
            root.TryGetProperty("mousePaw", out var mousePaw) ? Point(mousePaw) : null,
            root.TryGetProperty("tail", out var tail) ? Point(tail) : null,
            root.TryGetProperty("perched", out var perched) && perched.GetBoolean(),
            root.TryGetProperty("reach", out var reach) ? Point(reach) : null,
            root.TryGetProperty("taps", out var taps) && taps.GetBoolean(),
            root.TryGetProperty("pawLift", out var lift) ? lift.GetInt32() : 0);
    }

    // float3, float4... continue the float1 -> float2 step (sprite and bar alike) while both stay on the canvas.
    static void StackFloats(Dictionary<string, Place> places)
    {
        if (!places.TryGetValue("float1", out var first) || !places.TryGetValue("float2", out var second)) return;
        var step = new PixelPoint(second.Points[0].X - first.Points[0].X, second.Points[0].Y - first.Points[0].Y);
        if (step == default) return;
        var last = second;
        for (int n = 3; ; n++)
        {
            var next = last with
            {
                Points = last.Points.Select(p => Shift(p, step)).ToList(),
                Bar = Shift(last.Bar, step),
            };
            if (next.Points.Append(next.Bar).Any(p => p.X < 0 || p.Y < 0)) return;
            places.TryAdd($"{FloatPrefix}{n}", next);   // a skin may place its own
            last = places[$"{FloatPrefix}{n}"];
        }
    }

    static PixelPoint Shift(PixelPoint p, PixelPoint by) => new(p.X + by.X, p.Y + by.Y);

    static PixelPoint Point(JsonElement e) => new(e[0].GetInt32(), e[1].GetInt32());
}
