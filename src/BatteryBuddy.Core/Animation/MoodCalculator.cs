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
}
