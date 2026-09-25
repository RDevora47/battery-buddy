/// <summary>
/// A looping animated GIF of ARGB frames. Pixels with alpha below half are transparent; the rest are opaque.
/// Every frame uses one shared palette (index 0 = transparent), so a GIF holds at most 255 distinct colors.
/// </summary>
static class Gif
{
    public static byte[] Encode(int width, int height, IReadOnlyList<uint[]> frames, int delayCentiseconds)
    {
        var palette = new List<uint> { 0 };
        var index = new Dictionary<uint, byte>();
        var indexed = frames.Select(frame => frame.Select(c =>
        {
            if (c >> 24 < 0x80) return (byte)0;
            uint rgb = c & 0xFFFFFF;
            if (!index.TryGetValue(rgb, out var i))
            {
                if (palette.Count == 256) throw new InvalidOperationException("more than 255 colors in one GIF");
                index[rgb] = i = (byte)palette.Count;
                palette.Add(rgb);
            }
            return i;
        }).ToArray()).ToList();

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("GIF89a"u8);
        w.Write((ushort)width); w.Write((ushort)height);
        w.Write((byte)0xF7); w.Write((byte)0); w.Write((byte)0);   // 256-entry global palette
        for (int i = 0; i < 256; i++)
        {
            uint rgb = i < palette.Count ? palette[i] : 0;
            w.Write((byte)(rgb >> 16)); w.Write((byte)(rgb >> 8)); w.Write((byte)rgb);
        }
        w.Write(new byte[] { 0x21, 0xFF, 0x0B }); w.Write("NETSCAPE2.0"u8); w.Write(new byte[] { 3, 1, 0, 0, 0 });   // loop forever

        foreach (var pixels in indexed)
        {
            // Disposal 2 clears each frame before the next, so transparent pixels don't show the last one.
            w.Write(new byte[] { 0x21, 0xF9, 4, 0x09 }); w.Write((ushort)delayCentiseconds); w.Write(new byte[] { 0, 0 });
            w.Write((byte)0x2C); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)width); w.Write((ushort)height); w.Write((byte)0);
            w.Write((byte)8);
            var data = Lzw(pixels);
            for (int i = 0; i < data.Length; i += 255)
            {
                int n = Math.Min(255, data.Length - i);
                w.Write((byte)n); w.Write(data, i, n);
            }
            w.Write((byte)0);
        }
        w.Write((byte)0x3B);
        return ms.ToArray();
    }

    // Variable-length LZW with 8-bit symbols, restarting the table when it fills.
    static byte[] Lzw(byte[] pixels)
    {
        const int clear = 256, end = 257;
        var output = new List<byte>();
        int bitBuffer = 0, bitCount = 0, codeSize = 9, next = end + 1;
        var table = new Dictionary<int, int>();

        void Emit(int code)
        {
            bitBuffer |= code << bitCount;
            bitCount += codeSize;
            while (bitCount >= 8) { output.Add((byte)bitBuffer); bitBuffer >>= 8; bitCount -= 8; }
        }

        Emit(clear);
        int prefix = pixels[0];
        for (int i = 1; i < pixels.Length; i++)
        {
            int key = prefix << 8 | pixels[i];
            if (table.TryGetValue(key, out var code)) { prefix = code; continue; }
            Emit(prefix);
            if (next == 4096)
            {
                Emit(clear);
                table.Clear();
                next = end + 1;
                codeSize = 9;
            }
            else
            {
                if (next >= 1 << codeSize) codeSize++;
                table[key] = next++;
            }
            prefix = pixels[i];
        }
        Emit(prefix);
        Emit(end);
        if (bitCount > 0) output.Add((byte)bitBuffer);
        return output.ToArray();
    }
}
