namespace BatteryBuddy.Pet.Scene;

/// <summary>
/// Where the channel bubbles sit around a mouse, in art pixels (top-left of each 6x6 bubble): an arc over the
/// mouse sprite's top-left, left / top / right. The arc shifts sideways as a whole to stay inside the frame.
/// </summary>
public static class ChannelArc
{
    public const int BubbleSize = 6;
    public const int MaxChannels = 3;
    const int Side = 11, Rise = 8, TopRise = 15;

    static readonly PixelPoint[] One = { new(0, -TopRise) };
    static readonly PixelPoint[] Two = { new(-Side, -Rise), new(Side, -Rise) };
    static readonly PixelPoint[] Three = { new(-Side, -Rise), new(0, -TopRise), new(Side, -Rise) };

    /// <summary>Frame width (art pixels) that fits the right bubble of an arc over a mouse at <paramref name="mouse"/>, plus a 1px margin.</summary>
    public static int RequiredWidth(PixelPoint mouse) => mouse.X + Side + BubbleSize + 1;

    public static IReadOnlyList<PixelPoint> Positions(PixelPoint mouse, int count, int frameWidth)
    {
        var offsets = Math.Min(count, MaxChannels) switch { 1 => One, 2 => Two, _ => Three };
        var points = offsets.Select(o => new PixelPoint(mouse.X + o.X, Math.Max(0, mouse.Y + o.Y))).ToList();
        int shift = Math.Max(0, -points.Min(p => p.X)) - Math.Max(0, points.Max(p => p.X) + BubbleSize - frameWidth);
        return points.Select(p => p with { X = p.X + shift }).ToList();
    }
}
