using BatteryBuddy.Backends.Logitech;
using BatteryBuddy.Backends.Logitech.Hidpp;
using BatteryBuddy.Devices;

namespace BatteryBuddy.Tests;

public class HidppTests
{
    static byte[] Report(params byte[] bytes)
    {
        var report = new byte[HidppProtocol.LongLength];
        bytes.CopyTo(report, 0);
        return report;
    }

    [Fact]
    public void Request_is_a_long_report_with_our_software_id() =>
        Assert.Equal(Report(0x11, 0xFF, 0x08, 0x1B, 0x42), HidppProtocol.Request(0xFF, 0x08, 1, 0x42));

    [Fact]
    public void Feature_lookup_asks_the_root_for_the_feature_id() =>
        Assert.Equal(Report(0x11, 0xFF, 0x00, 0x0B, 0x10, 0x04), HidppProtocol.FeatureLookup(0xFF, HidppProtocol.UnifiedBattery));

    // Captured from an MX Master 3S over BLE: 60 %, full-level flag, charging, wired power.
    [Fact]
    public void Matches_our_reply_and_ignores_options_plus_traffic()
    {
        Assert.Equal(HidppMatch.Response, HidppProtocol.Match(Report(0x11, 0xFF, 0x08, 0x1B, 0x3C, 0x08, 0x01, 0x01), 0xFF, 0x08, 1));
        Assert.Equal(HidppMatch.Unrelated, HidppProtocol.Match(Report(0x11, 0xFF, 0x08, 0x1A, 0x3C), 0xFF, 0x08, 1));   // swId 0xA
        Assert.Equal(HidppMatch.Unrelated, HidppProtocol.Match(Report(0x11, 0x01, 0x08, 0x1B, 0x3C), 0xFF, 0x08, 1));   // other device
    }

    [Fact]
    public void Error_replies_from_device_and_receiver_are_recognised()
    {
        Assert.Equal(HidppMatch.Error, HidppProtocol.Match(Report(0x11, 0xFF, 0xFF, 0x08, 0x1B, 0x02), 0xFF, 0x08, 1));
        Assert.Equal(HidppMatch.Error, HidppProtocol.Match(Report(0x10, 0x02, 0x8F, 0x00, 0x0B, 0x09), 0x02, 0x00, 0));
    }

    [Fact]
    public void Battery_events_are_function_zero_software_id_zero()
    {
        Assert.True(HidppProtocol.IsEvent(Report(0x11, 0xFF, 0x08, 0x00, 0x3D, 0x08, 0x01, 0x01), 0xFF, 0x08));
        Assert.False(HidppProtocol.IsEvent(Report(0x11, 0xFF, 0x08, 0x1B, 0x3D), 0xFF, 0x08));
        Assert.False(HidppProtocol.IsEvent(Report(0x11, 0xFF, 0x09, 0x00, 0x05), 0xFF, 0x08));   // button event
    }

    [Fact]
    public void Receiver_connection_notices_are_recognised() =>
        Assert.True(HidppProtocol.IsConnectionNotice(Report(0x10, 0x01, 0x41, 0x04, 0x61, 0x7B, 0x40)));

    [Theory]
    [InlineData(new byte[] { 0x3C, 0x08, 0x01, 0x01 }, 60, true)]    // charging (live capture)
    [InlineData(new byte[] { 0x3C, 0x04, 0x02, 0x01 }, 60, true)]    // slow charging
    [InlineData(new byte[] { 0x64, 0x08, 0x03, 0x01 }, 100, false)]  // complete
    [InlineData(new byte[] { 0x21, 0x04, 0x00, 0x00 }, 33, false)]   // discharging
    [InlineData(new byte[] { 0x00, 0x02, 0x00, 0x00 }, 20, false)]   // no percentage: from the "low" flag
    public void Unified_battery_status(byte[] p, int percent, bool charging) =>
        Assert.Equal(new BatteryState(percent, charging), HidppBattery.ParseUnified(p));

    [Theory]
    [InlineData(new byte[] { 50, 20, 1 }, 50, true)]
    [InlineData(new byte[] { 90, 50, 2 }, 90, true)]
    [InlineData(new byte[] { 0, 0, 3 }, 100, false)]
    [InlineData(new byte[] { 40, 20, 4 }, 40, true)]
    [InlineData(new byte[] { 40, 20, 0 }, 40, false)]
    public void Battery_status(byte[] p, int percent, bool charging) =>
        Assert.Equal(new BatteryState(percent, charging), HidppBattery.ParseStatus(p));

    [Fact]
    public void Garbage_status_is_rejected()
    {
        Assert.Null(HidppBattery.ParseUnified(new byte[] { 0, 0, 0 }));
        Assert.Null(HidppBattery.ParseStatus(new byte[] { 150, 0, 0 }));
    }

    [Fact]
    public void Ble_address_comes_from_the_hid_path() =>
        Assert.Equal("DF43C7BC3A2B", LogitechSource.AddressFromPath(
            @"\\?\hid#{00001812-0000-1000-8000-00805f9b34fb}_dev_vid&02046d_pid&b034_rev&0006_df43c7bc3a2b&col02#9&3b92a28&0&0001#{4d1e55b2-f16f-11cf-88cb-001111000030}"));

    [Fact]
    public void Receiver_paths_have_no_address() =>
        Assert.Null(LogitechSource.AddressFromPath(@"\\?\hid#vid_046d&pid_c547&mi_02&col02#8&1a2b3c&0&0001#{4d1e55b2-f16f-11cf-88cb-001111000030}"));

    [Theory]
    [InlineData(3, DeviceKind.Mouse)]
    [InlineData(0, DeviceKind.Keyboard)]
    [InlineData(12, DeviceKind.Gamepad)]
    [InlineData(9, null)]
    public void Device_types_map_to_kinds(byte type, DeviceKind? kind) =>
        Assert.Equal(kind, HidppBattery.KindFromType(type));
}
