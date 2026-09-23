using BatteryBuddy.Core.Devices;

namespace BatteryBuddy.Core.Tests;

public class DeviceKindClassifierTests
{
    [Theory]
    [InlineData("MX Master 3S", DeviceKind.Mouse)]
    [InlineData("RK-S98RGB", DeviceKind.Keyboard)]
    [InlineData("Xbox Wireless Controller", DeviceKind.Gamepad)]
    [InlineData("Buds3 Pro de Roberto", DeviceKind.Earbuds)]
    [InlineData("WF-C500", DeviceKind.Earbuds)]
    [InlineData("S25 Ultra de Roberto", DeviceKind.Phone)]
    [InlineData("QUE5-L10063", DeviceKind.Other)]
    public void Classifies_known_devices_by_name(string name, DeviceKind expected) =>
        Assert.Equal(expected, DeviceKindClassifier.Classify(name, null));

    [Theory]
    [InlineData(0x5A020Cu, DeviceKind.Phone)]
    [InlineData(0x240404u, DeviceKind.Earbuds)]
    [InlineData(0x002580u, DeviceKind.Mouse)]
    [InlineData(0x002540u, DeviceKind.Keyboard)]
    [InlineData(0x002508u, DeviceKind.Gamepad)]
    public void Class_of_device_wins_over_name(uint cod, DeviceKind expected) =>
        Assert.Equal(expected, DeviceKindClassifier.Classify("Unnamed thing", cod));

    [Fact]
    public void Unknown_class_of_device_falls_back_to_name() =>
        Assert.Equal(DeviceKind.Mouse, DeviceKindClassifier.Classify("MX Master 3S", 0x10010C));
}
