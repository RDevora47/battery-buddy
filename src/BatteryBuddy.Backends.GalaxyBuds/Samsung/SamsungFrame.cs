namespace BatteryBuddy.Backends.GalaxyBuds.Samsung;

public sealed record SamsungFrame(byte MessageId, byte[] Payload, ushort Flags);
