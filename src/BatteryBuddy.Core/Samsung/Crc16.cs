namespace BatteryBuddy.Core.Samsung;

/// <summary>CRC16-XMODEM (poly 0x1021, init 0), as used by Galaxy Buds frames.</summary>
public static class Crc16
{
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0;
        foreach (byte b in data)
        {
            crc ^= (ushort)(b << 8);
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }
}
