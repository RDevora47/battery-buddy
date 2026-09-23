namespace BatteryBuddy.Core.Scene;

public static class BatteryBar
{
    public const int Width = 11;         // 4 segments of 2px with 1px gaps
    public const int Height = 2;
    public const int SegmentCount = 4;
    public const int LowAtOrBelow = 20;
    public const int CriticalAtOrBelow = 10;

    public const uint Green = 0xFF3DDC84;
    public const uint Amber = 0xFFFFB020;
    public const uint Red = 0xFFFF4D4D;
    public const uint Unknown = 0xFF8890A0;
    public const uint Empty = 0xFF3A3D46;

    public static uint ColorFor(int? percent) => percent switch
    {
        null => Unknown,
        > 50 => Green,
        > LowAtOrBelow => Amber,
        _ => Red,
    };

    /// <summary>1–25 → 1 … 76–100 → 4; unknown shows a full grey bar.</summary>
    public static int FilledSegments(int? percent) =>
        percent is int p ? Math.Clamp((p + 24) / 25, 0, SegmentCount) : SegmentCount;
}
