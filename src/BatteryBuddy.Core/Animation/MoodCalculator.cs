using BatteryBuddy.Core.Devices;
using BatteryBuddy.Core.Scene;

namespace BatteryBuddy.Core.Animation;

public static class MoodCalculator
{
    public static Mood From(IEnumerable<DeviceReading> devices)
    {
        int? lowest = devices.Where(d => d.IsConnected).Select(d => d.EffectiveBattery).Min();
        return lowest switch
        {
            null => Mood.Happy,
            <= BatteryBar.CriticalAtOrBelow => Mood.Worried,
            <= BatteryBar.LowAtOrBelow => Mood.Sleepy,
            _ => Mood.Happy,
        };
    }

    /// <summary>At least one connected device reports a battery, and every one that does is at 100 %.</summary>
    public static bool AllFull(IEnumerable<DeviceReading> devices)
    {
        var levels = devices.Where(d => d.IsConnected).Select(d => d.EffectiveBattery).OfType<int>().ToList();
        return levels.Count > 0 && levels.All(level => level == 100);
    }
}
