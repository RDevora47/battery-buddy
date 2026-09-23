namespace BatteryBuddy.Backends.Windows.Pnp;

/// <summary>One Windows device node, as read from the PnP property store.</summary>
public sealed record PnpNode(string InstanceId, string Name, int? Battery, bool? IsConnected, uint? ClassOfDevice);
