namespace BatteryBuddy.Pet.Scene;

/// <summary>
/// The "mini battery": a vertical 5-cell battery beside a device, drawn with half-size pixels (mixels).
/// Its 4x8 mixel grid is a nub row, a shell row, five cells (bottom = cell 1) and a shell row.
/// </summary>
public static class BatteryGauge
{
    public const int CellCount = 5;
    public const int MixelWidth = 4;
    public const int MixelHeight = 8;
    public const int ArtWidth = MixelWidth / 2;
    public const int ArtHeight = MixelHeight / 2;

    public const uint Shell = 0xFF1A1020;

    /// <summary>1–20 → 1 … 81–100 → 5; unknown shows every cell (in grey).</summary>
    public static int LitCells(int? percent) =>
        percent is int p ? Math.Clamp((p + 19) / 20, 0, CellCount) : CellCount;

    /// <summary>Color of mixel (x, y); 0 is transparent.</summary>
    public static uint MixelColor(int x, int y, int litCells, uint fill)
    {
        if (y == 0) return x is 1 or 2 ? Shell : 0;
        if (y == 1 || y == MixelHeight - 1 || x == 0 || x == MixelWidth - 1) return Shell;
        int cell = MixelHeight - 1 - y; // 1 at the bottom
        return cell <= litCells ? fill : BatteryBar.Empty;
    }
}
