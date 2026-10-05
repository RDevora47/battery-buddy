using System.Text;
using BatteryBuddy.Devices;

namespace BatteryBuddy.Pet.Ui;

public static class BubbleText
{
    public const string BluetoothOff = "Bluetooth is off";

    /// <param name="timeLeft">How much use is left, when known; shown unless the device is charging.</param>
    public static string For(DeviceReading device, DateTimeOffset now, TimeSpan? timeLeft = null)
    {
        var text = new StringBuilder();
        if (device.NoBattery) return $"{device.Name}{Environment.NewLine}no battery level";
        text.AppendLine(device.Name);
        if (device.Detail is BudsDetail buds)
        {
            text.AppendLine($"L {Percent(buds.Left)} · R {Percent(buds.Right)} · Case {Percent(buds.Case)}{Status(device, timeLeft)}");
            text.AppendLine((buds.LeftWorn, buds.RightWorn) switch
            {
                (true, true) => "wearing both",
                (true, false) => "wearing left",
                (false, true) => "wearing right",
                _ => "not worn",
            });
        }
        else if (device.BatteryStale)
        {
            // When that reading was taken isn't known, so there's no "updated … ago".
            text.AppendLine($"Battery {Percent(device.BatteryPercent)} · last known");
            text.Append("last Bluetooth reading");
            return text.ToString();
        }
        else
        {
            text.AppendLine($"Battery {Percent(device.BatteryPercent)}{Status(device, timeLeft)}");
            if (device.DetailUnavailable)
                text.AppendLine("L/R detail unavailable");
        }
        text.Append($"updated {Ago(now - device.ReadAt)}");
        return text.ToString();
    }

    public static string Ago(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        if (elapsed.TotalSeconds < 60) return $"{(int)elapsed.TotalSeconds}s ago";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
        return $"{(int)elapsed.TotalHours}h ago";
    }

    static string Percent(int? value) => value is int v ? $"{v}%" : "?";

    /// <summary>"45min", "3h 10min", "2d 5h": days and hours, hours and minutes, or minutes alone.</summary>
    public static string TimeLeft(TimeSpan left)
    {
        int minutes = (int)Math.Round(left.TotalMinutes);
        if (minutes < 1) return "<1min";
        if (minutes < 60) return $"{minutes}min";
        if (minutes < 24 * 60) return minutes % 60 == 0 ? $"{minutes / 60}h" : $"{minutes / 60}h {minutes % 60}min";
        int hours = (int)Math.Round(left.TotalHours);
        return hours % 24 == 0 ? $"{hours / 24}d" : $"{hours / 24}d {hours % 24}h";
    }

    static string Status(DeviceReading device, TimeSpan? timeLeft) =>
        device.IsCharging ? " · charging"
        : timeLeft is TimeSpan left ? $" · {TimeLeft(left)} left"
        : "";
}
