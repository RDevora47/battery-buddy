namespace BatteryBuddy.Core.Devices;

public sealed record BudsDetail(int? Left, int? Right, int? Case, bool LeftWorn, bool RightWorn)
{
    public int? Lowest => (Left, Right) switch
    {
        (int l, int r) => Math.Min(l, r),
        (int l, null) => l,
        (null, int r) => r,
        _ => null,
    };
}
