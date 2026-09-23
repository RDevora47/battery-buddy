using System.Text.RegularExpressions;

namespace BatteryBuddy.Core.Devices;

public static class DeviceKindClassifier
{
    static readonly (Regex Pattern, DeviceKind Kind)[] NameRules =
    {
        (Rule(@"controller|gamepad|dualsense|joy-?con"), DeviceKind.Gamepad),
        (Rule(@"buds|airpods|earbud|headphone|headset|\bw[fh]-"), DeviceKind.Earbuds),
        (Rule(@"mouse|\bmx (master|anywhere|ergo)"), DeviceKind.Mouse),
        (Rule(@"keyboard|\brk-|\bkeys\b"), DeviceKind.Keyboard),
        (Rule(@"phone|\bs\d{2}\b|pixel|galaxy [asz]\d"), DeviceKind.Phone),
    };

    public static DeviceKind Classify(string name, uint? classOfDevice) =>
        classOfDevice is uint cod && FromClassOfDevice(cod) is DeviceKind kind ? kind : FromName(name);

    static DeviceKind? FromClassOfDevice(uint cod)
    {
        uint major = (cod >> 8) & 0x1F;
        uint minor = (cod >> 2) & 0x3F;
        return major switch
        {
            0x02 => DeviceKind.Phone,
            0x04 => DeviceKind.Earbuds,
            0x05 => ((minor >> 4) & 0x3) switch
            {
                0x1 => DeviceKind.Keyboard,
                0x2 => DeviceKind.Mouse,
                _ => (minor & 0xF) is 0x1 or 0x2 ? DeviceKind.Gamepad : null,
            },
            _ => null,
        };
    }

    static DeviceKind FromName(string name)
    {
        foreach (var (pattern, kind) in NameRules)
            if (pattern.IsMatch(name)) return kind;
        return DeviceKind.Other;
    }

    static Regex Rule(string pattern) => new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
