using BatteryBuddy.Backends.Windows.Input;
using BatteryBuddy.Devices;

namespace BatteryBuddy.Tests;

public class InputDeviceMergerTests
{
    static readonly Guid Laptop = InputDeviceMerger.LocalMachineContainer;
    static readonly Guid RkBluetooth = new("030875f0-2cd4-5d0e-9386-58b685cb0c2c");
    static readonly Guid MxMaster = new("3b781db3-d37a-525f-bd11-347071cf5d6a");
    static readonly Guid RkDongle = new("ec7a8ab7-b839-11f1-a643-b31286009302");
    static readonly Guid Lightspeed = new("0eddb0bb-ac7c-11f1-a63c-94b6098e87e8");
    static readonly Guid G502Unplugged = new("f5b65544-8cc2-5e53-9138-e2413d6e98ab");

    static InputNode Keyboard(string id, Guid container, string name, bool present = true) =>
        new(id, DeviceKind.Keyboard, container, name, present);

    static InputNode Mouse(string id, Guid container, string name, bool present = true) =>
        new(id, DeviceKind.Mouse, container, name, present);

    // The keyboard and mouse nodes found on 2026-09-24.
    static readonly InputNode[] Scan =
    {
        Keyboard(@"HID\VID_0B05&PID_19B6&MI_00&Col02\7&4322c2e&0&0001", Laptop, "DEVORA"),
        Mouse(@"HID\ASUF1209&Col01\5&2a62a8f2&0&0000", Laptop, "DEVORA"),
        Keyboard(@"HID\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&02046d_PID&b35a_REV&0011_ff03000652c0&Col02\9&2fab236&0&0001", RkBluetooth, "RK-S98RGB"),
        Keyboard(@"HID\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&02046d_PID&b35a_REV&0011_ff03000652c0&Col01\9&2fab236&0&0000", RkBluetooth, "RK-S98RGB"),
        Mouse(@"HID\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&02046d_PID&b35a_REV&0011_ff03000652c0&Col07\9&2fab236&0&0006", RkBluetooth, "RK-S98RGB"),
        Mouse(@"HID\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&02046d_PID&b034_REV&0006_df43c7bc3a2b&Col01\9&3b92a28&0&0000", MxMaster, "MX Master 3S"),
        Keyboard(@"HID\VID_258A&PID_0150&MI_00\7&127ab62d&0&0000", RkDongle, "Gaming KB"),
        Keyboard(@"HID\VID_258A&PID_0150&MI_01&Col05\7&25aad26b&0&0004", RkDongle, "Gaming KB"),
        Mouse(@"HID\VID_258A&PID_0150&MI_01&Col09\7&25aad26b&0&0008", RkDongle, "Gaming KB"),
        Keyboard(@"HID\VID_046D&PID_C547&MI_01&Col01\7&e6a1a35&0&0000", Lightspeed, "LIGHTSPEED Receiver"),
        Mouse(@"HID\VID_046D&PID_C547&MI_00\7&262da67a&0&0000", Lightspeed, "LIGHTSPEED Receiver"),
        Mouse(@"HID\VID_046D&PID_C095&MI_00\7&2357134c&0&0000", G502Unplugged, "G502 X PLUS", present: false),
    };

    static IReadOnlyDictionary<string, DeviceReading> MergeScan() =>
        InputDeviceMerger.Merge(Scan, TestReadings.T0).ToDictionary(r => r.Name);

    [Fact]
    public void One_reading_per_attached_external_device()
    {
        Assert.Equal(new[] { "Gaming KB", "LIGHTSPEED Receiver", "MX Master 3S", "RK-S98RGB" }, MergeScan().Keys.OrderBy(n => n));
    }

    [Fact]
    public void Readings_are_connected_without_a_battery()
    {
        Assert.All(MergeScan().Values, r =>
        {
            Assert.True(r.IsConnected);
            Assert.True(r.NoBattery);
            Assert.Null(r.BatteryPercent);
            Assert.Equal(InputDeviceMerger.SourceName, r.Source);
            Assert.Equal(DeviceKey.ForName(r.Name), r.Key);
        });
    }

    [Fact]
    public void The_name_decides_the_kind_of_a_device_with_both_collections()
    {
        var scan = MergeScan();
        Assert.Equal(DeviceKind.Keyboard, scan["RK-S98RGB"].Kind);
        Assert.Equal(DeviceKind.Keyboard, scan["Gaming KB"].Kind);
        Assert.Equal(DeviceKind.Mouse, scan["MX Master 3S"].Kind);
    }

    [Fact]
    public void Without_a_telling_name_the_first_interface_decides() =>
        Assert.Equal(DeviceKind.Mouse, MergeScan()["LIGHTSPEED Receiver"].Kind);

    [Fact]
    public void Without_a_telling_name_the_first_collection_decides()
    {
        var box = Guid.NewGuid();
        var reading = Assert.Single(InputDeviceMerger.Merge(new[]
        {
            Mouse(@"HID\VID_1234&PID_0001&Col03\1&1&0&0002", box, "Widget"),
            Keyboard(@"HID\VID_1234&PID_0001&Col01\1&1&0&0000", box, "Widget"),
        }, TestReadings.T0));
        Assert.Equal(DeviceKind.Keyboard, reading.Kind);
    }

    [Fact]
    public void Bluetooth_devices_carry_their_address_so_they_merge_with_battery_readings()
    {
        var scan = MergeScan();
        Assert.Equal("FF03000652C0", scan["RK-S98RGB"].Address);
        Assert.Equal("DF43C7BC3A2B", scan["MX Master 3S"].Address);
        Assert.Null(scan["Gaming KB"].Address);
    }

    [Fact]
    public void A_device_named_as_something_else_is_left_out() =>
        Assert.Empty(InputDeviceMerger.Merge(
            new[] { Keyboard(@"HID\VID_045E&PID_0B13&Col01\1&1&0&0000", Guid.NewGuid(), "Xbox Wireless Controller") },
            TestReadings.T0));

    [Fact]
    public void A_nameless_device_is_named_after_its_kind() =>
        Assert.Equal("Keyboard", Assert.Single(InputDeviceMerger.Merge(
            new[] { Keyboard(@"HID\VID_1234&PID_0001\1&1&0&0000", Guid.NewGuid(), null!) }, TestReadings.T0)).Name);
}
