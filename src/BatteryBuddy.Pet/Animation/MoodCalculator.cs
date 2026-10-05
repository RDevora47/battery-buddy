using BatteryBuddy.Devices;
using BatteryBuddy.Pet.Scene;

namespace BatteryBuddy.Pet.Animation;

public static class MoodCalculator
{
    /// <summary>
    /// Lowest battery among connected devices that report one and aren't charging; null if none do.
    /// A device on its charger is being looked after, so it doesn't make the pet sad.
    /// </summary>
    public static int? Lowest(IEnumerable<DeviceReading> devices) =>
        devices.Where(d => d.IsConnected).Select(Uncharged).Min();

    /// <summary>A connected device that isn't charging is at <see cref="BatteryBar.CriticalAtOrBelow"/> % or less.</summary>
    public static bool HasCritical(IEnumerable<DeviceReading> devices) => Lowest(devices) <= BatteryBar.CriticalAtOrBelow;

    // The level that counts toward the mood: none while charging, except that a bud in the ear still counts
    // when only its partner is in the case.
    static int? Uncharged(DeviceReading d) => d switch
    {
        { Detail: { AnyInCase: true } buds } => new[] { buds.LeftInCase ? null : buds.Left, buds.RightInCase ? null : buds.Right }.Min(),
        { IsCharging: true } => null,
        _ => d.EffectiveBattery,
    };

    public static Mood From(IEnumerable<DeviceReading> devices) => Lowest(devices) switch
    {
        null => Mood.Happy,
        <= BatteryBar.CriticalAtOrBelow => Mood.Worried,
        <= BatteryBar.LowAtOrBelow => Mood.Sleepy,
        _ => Mood.Happy,
    };

    /// <summary>Gills perk up with charge: perky above 50 %, droopy down to the low threshold, limp at or below it.</summary>
    public static string GillsFor(int? lowest) => lowest switch
    {
        null or > 50 => "gills_perky",
        > BatteryBar.LowAtOrBelow => "gills_droopy",
        _ => "gills_limp",
    };

    /// <summary>Color drains linearly from 50 % down to 0 %, up to <see cref="MaxFade"/>.</summary>
    public static double FadeFor(int? lowest) =>
        lowest is int level && level < 50 ? MaxFade * (50 - Math.Max(level, 0)) / 50.0 : 0;

    public const double MaxFade = 0.7;

    public const int SaiyanAtOrAbove = 95;

    /// <summary>At least one connected device reports a battery, and every one that does is at <see cref="SaiyanAtOrAbove"/> % or more.</summary>
    public static bool SuperSaiyan(IEnumerable<DeviceReading> devices)
    {
        var levels = devices.Where(d => d.IsConnected).Select(d => d.EffectiveBattery).OfType<int>().ToList();
        return levels.Count > 0 && levels.All(level => level >= SaiyanAtOrAbove);
    }
}
