namespace BatteryBuddy.Core.Samsung;

public static class FrameEncoder
{
    const ushort ResponseFlag = 0x1000;

    public static byte[] Encode(byte messageId, ReadOnlySpan<byte> payload, ushort flags = 0)
    {
        int length = 1 + payload.Length + 2;
        var frame = new byte[3 + length + 1];
        ushort header = (ushort)(flags | length);
        frame[0] = 0xFD;
        frame[1] = (byte)header;
        frame[2] = (byte)(header >> 8);
        frame[3] = messageId;
        payload.CopyTo(frame.AsSpan(4));
        ushort crc = Crc16.Compute(frame.AsSpan(3, 1 + payload.Length));
        frame[4 + payload.Length] = (byte)crc;
        frame[5 + payload.Length] = (byte)(crc >> 8);
        frame[^1] = 0xDD;
        return frame;
    }

    /// <summary>The acknowledgement QuickControls sends after EXTENDED_STATUS_UPDATED.</summary>
    public static byte[] ExtendedStatusAck() =>
        Encode(StatusParser.ExtendedStatusUpdated, new byte[] { 0x00 }, ResponseFlag);
}
