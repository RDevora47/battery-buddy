using BatteryBuddy.Devices;

namespace BatteryBuddy.Backends.Windows.Input;

/// <summary>
/// One keyboard or mouse device node (a HID collection), as Windows lists it. Class: which setup class it's in.
/// ContainerId groups the nodes of one physical device (or USB receiver); ContainerName is that device's name.
/// </summary>
public sealed record InputNode(string InstanceId, DeviceKind Class, Guid? ContainerId, string? ContainerName, bool IsPresent);
