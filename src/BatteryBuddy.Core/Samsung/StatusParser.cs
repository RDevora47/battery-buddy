using BatteryBuddy.Core.Devices;

namespace BatteryBuddy.Core.Samsung;

public static class StatusParser
{
    public const byte StatusUpdated = 0x60;
    public const byte ExtendedStatusUpdated = 0x61;

    // Layout after the leading fields: left%, right%, coupled, mainConnection, wearState, case%.
    // 0x60 has 1 leading byte (revision); 0x61 has 2 (revision, earType).
    public static BudsDetail? TryParse(SamsungFrame frame)
    {
        int offset = frame.MessageId switch
        {
            StatusUpdated => 1,
            ExtendedStatusUpdated => 2,
            _ => -1,
        };
        var p = frame.Payload;
        if (offset < 0 || p.Length < offset + 6) return null;

        byte wear = p[offset + 4]; // high nibble = left, low nibble = right; 1 = wearing
        return new BudsDetail(
            Left: Percent(p[offset]),
            Right: Percent(p[offset + 1]),
            Case: Percent(p[offset + 5]),
            LeftWorn: (wear >> 4) == 1,
            RightWorn: (wear & 0x0F) == 1);
    }

    // 0 is reported when a component can't be read (e.g. case while buds are out).
    static int? Percent(byte value) => value is >= 1 and <= 100 ? value : null;
}
