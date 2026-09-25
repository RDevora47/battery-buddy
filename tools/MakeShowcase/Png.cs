using System.IO.Compression;

/// <summary>An 8-bit RGBA PNG of ARGB pixels.</summary>
static class Png
{
    public static void Write(string path, int width, int height, uint[] argb)
    {
        var raw = new MemoryStream();
        for (int y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            for (int x = 0; x < width; x++)
            {
                uint c = argb[y * width + x];
                raw.WriteByte((byte)(c >> 16)); raw.WriteByte((byte)(c >> 8)); raw.WriteByte((byte)c); raw.WriteByte((byte)(c >> 24));
            }
        }
        var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.SmallestSize, leaveOpen: true)) { raw.Position = 0; raw.CopyTo(zs); }

        using var png = File.Create(path);
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        void Chunk(string type, byte[] data)
        {
            var body = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
            png.Write(BigEndian(data.Length));
            png.Write(body);
            png.Write(BigEndian((int)~Crc(body)));
        }
        Chunk("IHDR", BigEndian(width).Concat(BigEndian(height)).Concat(new byte[] { 8, 6, 0, 0, 0 }).ToArray());
        Chunk("IDAT", z.ToArray());
        Chunk("IEND", Array.Empty<byte>());
    }

    static byte[] BigEndian(int v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };

    static uint Crc(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        return crc;
    }
}
