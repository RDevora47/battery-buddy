namespace BatteryBuddy.Devices;

public sealed record BudsDetail(
    int? Left,
    int? Right,
    int? Case,
    bool LeftWorn,
    bool RightWorn,
    bool LeftInCase = false,
    bool RightInCase = false)
{
    public int? Lowest => Min(Left, Right);

    /// <summary>The lower of the buds out of the case (the ones being used up); null with both in it.</summary>
    public int? LowestOutOfCase => Min(LeftInCase ? null : Left, RightInCase ? null : Right);

    /// <summary>A bud sitting in the case is being charged by it.</summary>
    public bool AnyInCase => LeftInCase || RightInCase;

    static int? Min(int? a, int? b) => (a, b) switch
    {
        (int l, int r) => Math.Min(l, r),
        (int l, null) => l,
        (null, int r) => r,
        _ => null,
    };
}
