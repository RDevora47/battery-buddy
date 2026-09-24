namespace BatteryBuddy.Pet.Ui;

/// <summary>Keeps rectangles fully inside a monitor's work area.</summary>
public static class ScreenClamp
{
    /// <summary>The area containing the point, else the one nearest to it.</summary>
    public static ScreenRect AreaFor(double x, double y, IReadOnlyList<ScreenRect> areas) =>
        areas.FirstOrDefault(a => a.Contains(x, y), areas.MinBy(a => DistanceSquared(a, x, y)));

    /// <summary>Offset that moves rect inside area; a rect bigger than the area is pinned to its top-left.</summary>
    public static (double Dx, double Dy) Into(ScreenRect rect, ScreenRect area) =>
        (Axis(rect.X, rect.Width, area.X, area.Width), Axis(rect.Y, rect.Height, area.Y, area.Height));

    static double Axis(double start, double length, double areaStart, double areaLength) =>
        Math.Max(areaStart, Math.Min(start, areaStart + areaLength - length)) - start;

    static double DistanceSquared(ScreenRect a, double x, double y)
    {
        double dx = Math.Max(Math.Max(a.X - x, 0), x - (a.X + a.Width));
        double dy = Math.Max(Math.Max(a.Y - y, 0), y - (a.Y + a.Height));
        return dx * dx + dy * dy;
    }
}
