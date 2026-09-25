namespace BatteryBuddy.Backends.Windows.Pnp;

/// <summary>One Windows device node, as read from the PnP property store. IsPresent: whether the hardware is plugged in.</summary>
public sealed record PnpNode(string InstanceId, string Name, int? Battery, bool? IsConnected, uint? ClassOfDevice, bool? IsPresent = null);
