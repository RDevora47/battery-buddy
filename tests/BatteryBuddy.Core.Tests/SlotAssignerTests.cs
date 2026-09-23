using BatteryBuddy.Core.Devices;
using BatteryBuddy.Core.Scene;

namespace BatteryBuddy.Core.Tests;

public class SlotAssignerTests
{
    static Dictionary<string, string> PlacesOf(params DeviceReading[] devices) =>
        SlotAssigner.Assign(devices).ToDictionary(p => p.Device.Name, p => p.PlaceName);

    [Fact]
    public void Each_kind_gets_its_place()
    {
        var places = PlacesOf(
            TestReadings.Make("Buds", DeviceKind.Earbuds),
            TestReadings.Make("Mouse", DeviceKind.Mouse),
            TestReadings.Make("Keys", DeviceKind.Keyboard),
            TestReadings.Make("Phone", DeviceKind.Phone),
            TestReadings.Make("Thing", DeviceKind.Other));
        Assert.Equal("gills", places["Buds"]);
        Assert.Equal("righthand", places["Mouse"]);
        Assert.Equal("seat", places["Keys"]);
        Assert.Equal("side", places["Phone"]);
        Assert.Equal("float1", places["Thing"]);
    }

    [Fact]
    public void Gamepad_is_held_in_both_hands() =>
        Assert.Equal("hands", PlacesOf(TestReadings.Make("Pad", DeviceKind.Gamepad))["Pad"]);

    [Fact]
    public void Mouse_in_right_hand_and_gamepad_in_both_hands_coexist()
    {
        var places = PlacesOf(TestReadings.Make("Aaa Pad", DeviceKind.Gamepad), TestReadings.Make("Mouse", DeviceKind.Mouse));
        Assert.Equal("hands", places["Aaa Pad"]);
        Assert.Equal("righthand", places["Mouse"]);
    }

    [Fact]
    public void Second_gamepad_floats() =>
        Assert.Equal("float1", PlacesOf(TestReadings.Make("A Pad", DeviceKind.Gamepad), TestReadings.Make("B Pad", DeviceKind.Gamepad))["B Pad"]);

    [Fact]
    public void Second_earbuds_go_to_neck_and_float_uses_gadget_sprite()
    {
        var placements = SlotAssigner.Assign(new[]
        {
            TestReadings.Make("A Buds", DeviceKind.Earbuds),
            TestReadings.Make("B Buds", DeviceKind.Earbuds),
            TestReadings.Make("C Buds", DeviceKind.Earbuds),
        });
        Assert.Equal(new[] { "gills", "neck", "float1" }, placements.Select(p => p.PlaceName));
        Assert.Equal(new[] { "earbud", "earbud", "gadget" }, placements.Select(p => p.SpriteName));
    }

    [Fact]
    public void Overflow_devices_are_skipped()
    {
        var devices = Enumerable.Range(0, 5).Select(i => TestReadings.Make($"Thing {i}")).ToArray();
        Assert.Equal(2, SlotAssigner.Assign(devices).Count);
    }

    [Fact]
    public void Assignment_is_stable_regardless_of_input_order()
    {
        var a = TestReadings.Make("A Buds", DeviceKind.Earbuds);
        var b = TestReadings.Make("B Buds", DeviceKind.Earbuds);
        Assert.Equal(PlacesOf(a, b), PlacesOf(b, a));
    }

    [Fact]
    public void Disconnected_devices_are_not_placed() =>
        Assert.Empty(SlotAssigner.Assign(new[] { TestReadings.Make("Mouse", DeviceKind.Mouse, connected: false) }));
}
