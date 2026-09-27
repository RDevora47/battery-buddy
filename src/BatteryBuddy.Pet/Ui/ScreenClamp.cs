namespace BatteryBuddy.Pet.Ui;

/// <summary>Keeps rectangles inside a monitor's work area, optionally letting part of them hang off it.</summary>
public static class ScreenClamp
{
    /// <summary>The area containing the point, else the one nearest to it.</summary>
    public static ScreenRect AreaFor(double x, double y, IReadOnlyList<ScreenRect> areas) =>
        areas.FirstOrDefault(a => a.Contains(x, y), areas.MinBy(a => DistanceSquared(a, x, y)));

    /// <summary>
    /// Offset that moves rect inside area, allowing up to <paramref name="overhang"/> (a fraction of its size)
    /// to stick out past each edge; a rect that still doesn't fit is pinned to the area's top-left.
    /// </summary>
    public static (double Dx, double Dy) Into(ScreenRect rect, ScreenRect area, double overhang = 0) =>
        (Axis(rect.X, rect.Width, area.X, area.Width, overhang), Axis(rect.Y, rect.Height, area.Y, area.Height, overhang));

    static double Axis(double start, double length, double areaStart, double areaLength, double overhang) =>
        Math.Max(areaStart - overhang * length, Math.Min(start, areaStart + areaLength - (1 - overhang) * length)) - start;

    static double DistanceSquared(ScreenRect a, double x, double y)
    {
        double dx = Math.Max(Math.Max(a.X - x, 0), x - (a.X + a.Width));
        double dy = Math.Max(Math.Max(a.Y - y, 0), y - (a.Y + a.Height));
        return dx * dx + dy * dy;
    }
}
