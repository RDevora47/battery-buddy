using System.Text;
using BatteryBuddy.Devices;

namespace BatteryBuddy.Pet.Ui;

public static class BubbleText
{
    public const string BluetoothOff = "Bluetooth is off";

    public static string For(DeviceReading device, DateTimeOffset now)
    {
        var text = new StringBuilder();
        text.AppendLine(device.Name);
        if (device.Detail is BudsDetail buds)
        {
            text.AppendLine($"L {Percent(buds.Left)} · R {Percent(buds.Right)} · Case {Percent(buds.Case)}{Charging(device)}");
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
            text.AppendLine($"Battery {Percent(device.BatteryPercent)}{Charging(device)}");
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

    static string Charging(DeviceReading device) => device.IsCharging ? " · charging" : "";
}
