namespace BatteryBuddy.Core.Ui;

public readonly record struct ScreenRect(double X, double Y, double Width, double Height)
{
    public bool Contains(double x, double y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
}

public static class WindowPlacement
{
    public static (double X, double Y) Resolve(
        (double X, double Y)? saved,
        double width,
        double height,
        IReadOnlyList<ScreenRect> workAreas,
        ScreenRect primaryWorkArea,
        double margin = 8)
    {
        if (saved is (double x, double y) && workAreas.Any(a => a.Contains(x + width / 2, y + height / 2)))
            return (x, y);
        return (primaryWorkArea.X + primaryWorkArea.Width - width - margin,
                primaryWorkArea.Y + primaryWorkArea.Height - height - margin);
    }
}
