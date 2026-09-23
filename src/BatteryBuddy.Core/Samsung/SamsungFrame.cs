namespace BatteryBuddy.Core.Samsung;

public sealed record SamsungFrame(byte MessageId, byte[] Payload, ushort Flags);
