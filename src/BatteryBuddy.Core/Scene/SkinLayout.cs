using System.Text.Json;

namespace BatteryBuddy.Core.Scene;

public readonly record struct PixelPoint(int X, int Y);

/// <summary>Where a device goes: sprite drawn at each point, battery bar at Bar. FollowsBody = bobs with the pet.</summary>
public sealed record Place(IReadOnlyList<PixelPoint> Points, PixelPoint Bar, bool FollowsBody);

public sealed record SkinLayout(
    int CanvasWidth,
    int CanvasHeight,
    PixelPoint Body,
    IReadOnlyDictionary<string, Place> Places,
    IReadOnlyDictionary<string, PixelPoint> Overlays)
{
    public static readonly string[] RequiredPlaces = { "gills", "neck", "hands", "righthand", "seat", "side", "float1", "float2" };

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
                p.Value.GetProperty("followsBody").GetBoolean());
        }

        var overlays = root.GetProperty("overlays").EnumerateObject().ToDictionary(o => o.Name, o => Point(o.Value));

        return new SkinLayout(
            canvas.GetProperty("width").GetInt32(),
            canvas.GetProperty("height").GetInt32(),
            Point(root.GetProperty("body")),
            places,
            overlays);
    }

    static PixelPoint Point(JsonElement e) => new(e[0].GetInt32(), e[1].GetInt32());
}
