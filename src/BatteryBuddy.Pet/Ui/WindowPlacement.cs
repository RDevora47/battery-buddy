namespace BatteryBuddy.Pet.Ui;

public readonly record struct ScreenRect(double X, double Y, double Width, double Height)
{
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;

    public bool Contains(double x, double y) => x >= X && y >= Y && x < X + Width && y < Y + Height;

    public ScreenRect Offset(double dx, double dy) => this with { X = X + dx, Y = Y + dy };
}

public static class WindowPlacement
{
    /// <summary>
    /// Window position that keeps the pet (at <paramref name="pet"/> inside the window) fully on screen.
    /// A saved position whose monitor is gone falls back to the bottom-right of the primary work area.
    /// </summary>
    public static (double X, double Y) Resolve(
        (double X, double Y)? saved,
        ScreenRect pet,
        IReadOnlyList<ScreenRect> workAreas,
        ScreenRect primaryWorkArea,
        double margin = 8)
    {
        if (saved is (double x, double y))
        {
            var onScreen = pet.Offset(x, y);
            if (workAreas.Any(a => a.Contains(onScreen.CenterX, onScreen.CenterY)))
            {
                var (dx, dy) = ScreenClamp.Into(onScreen, ScreenClamp.AreaFor(onScreen.CenterX, onScreen.CenterY, workAreas));
                return (x + dx, y + dy);
            }
        }
        return (primaryWorkArea.X + primaryWorkArea.Width - margin - pet.X - pet.Width,
                primaryWorkArea.Y + primaryWorkArea.Height - margin - pet.Y - pet.Height);
    }
}

public static class BubblePlacement
{
    /// <summary>
    /// Speech bubble position: above the pet with right edges nearly aligned, below it when there is
    /// no room above, and always shifted to stay inside the work area.
    /// </summary>
    public static (double X, double Y) Resolve(ScreenRect pet, double width, double height, ScreenRect area,
        double gap = 6, double inset = 4)
    {
        double x = pet.X + pet.Width - inset - width;
        double y = pet.Y - gap - height;
        if (y < area.Y) y = pet.Y + pet.Height + gap;
        var (dx, dy) = ScreenClamp.Into(new ScreenRect(x, y, width, height), area);
        return (x + dx, y + dy);
    }
}
