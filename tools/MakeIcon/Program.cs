// Renders the pet's tray art (idle body + perky gills) into src/BatteryBuddy.App/app.ico, the .exe's icon.
// Run after changing the skin:  dotnet run --project tools/MakeIcon
using System.IO.Compression;
using BatteryBuddy.Pet.Scene;

var root = FindRoot();
var skin = Path.Combine(root, "src", "BatteryBuddy.App", "Skins", "axolotl", "sprites.txt");
var output = Path.Combine(root, "src", "BatteryBuddy.App", "app.ico");

var sprites = SpriteSheetParser.Parse(File.ReadAllText(skin));
var (size, pixels) = IconCanvas.Square(Stack(sprites["body_idle"], sprites["gills_perky"]));

// Bigger sizes repeat pixels so the art stays crisp; smaller ones average them.
var images = new[] { 256, 128, 64, 48, 32, 24, 16 }
    .Select(s => (s, s >= size ? Enlarge(pixels, size, s) : Shrink(pixels, size, s)))
    .ToList();
File.WriteAllBytes(output, Ico(images));
Console.WriteLine($"Wrote {output} ({string.Join(", ", images.Select(i => i.s))} px)");

static string FindRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "BatteryBuddy.sln"))) return dir.FullName;
    throw new InvalidOperationException("BatteryBuddy.sln not found above " + AppContext.BaseDirectory);
}

// Top drawn over bottom, both anchored top-left (as the tray icon does).
static Sprite Stack(Sprite bottom, Sprite top)
{
    int w = Math.Max(bottom.Width, top.Width), h = Math.Max(bottom.Height, top.Height);
    var px = new uint[w * h];
    foreach (var s in new[] { bottom, top })
        for (int y = 0; y < s.Height; y++)
            for (int x = 0; x < s.Width; x++)
                if (s.Pixels[y * s.Width + x] is var c and not 0) px[y * w + x] = c;
    return new Sprite(bottom.Name, w, h, px, bottom.Density);
}

// Nearest neighbour (the target is a multiple of the source, or close to one).
static uint[] Enlarge(uint[] src, int from, int to)
{
    var dst = new uint[to * to];
    for (int y = 0; y < to; y++)
        for (int x = 0; x < to; x++)
            dst[y * to + x] = src[y * from / to * from + x * from / to];
    return dst;
}

// Box filter weighted by alpha, so transparent pixels don't darken the edges.
static uint[] Shrink(uint[] src, int from, int to)
{
    var dst = new uint[to * to];
    for (int y = 0; y < to; y++)
        for (int x = 0; x < to; x++)
        {
            double a = 0, r = 0, g = 0, b = 0, n = 0;
            for (int sy = y * from / to; sy < (y + 1) * from / to; sy++)
                for (int sx = x * from / to; sx < (x + 1) * from / to; sx++)
                {
                    uint c = src[sy * from + sx];
                    double ca = (c >> 24) / 255.0;
                    a += ca; r += ca * (c >> 16 & 0xFF); g += ca * (c >> 8 & 0xFF); b += ca * (c & 0xFF); n++;
                }
            if (a == 0) continue;
            uint A = (uint)Math.Round(a / n * 255);
            dst[y * to + x] = A << 24 | (uint)Math.Round(r / a) << 16 | (uint)Math.Round(g / a) << 8 | (uint)Math.Round(b / a);
        }
    return dst;
}

// ICO with a PNG image per size.
static byte[] Ico(List<(int Size, uint[] Pixels)> images)
{
    var pngs = images.Select(i => Png(i.Size, i.Pixels)).ToList();
    using var ms = new MemoryStream();
    using var w = new BinaryWriter(ms);
    w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)images.Count);
    int offset = 6 + 16 * images.Count;
    for (int i = 0; i < images.Count; i++)
    {
        byte dim = (byte)(images[i].Size >= 256 ? 0 : images[i].Size);   // 0 means 256
        w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
        w.Write((ushort)1); w.Write((ushort)32);
        w.Write(pngs[i].Length); w.Write(offset);
        offset += pngs[i].Length;
    }
    foreach (var png in pngs) w.Write(png);
    return ms.ToArray();
}

static byte[] Png(int size, uint[] argb)
{
    var raw = new MemoryStream();
    for (int y = 0; y < size; y++)
    {
        raw.WriteByte(0);
        for (int x = 0; x < size; x++)
        {
            uint c = argb[y * size + x];
            raw.WriteByte((byte)(c >> 16)); raw.WriteByte((byte)(c >> 8)); raw.WriteByte((byte)c); raw.WriteByte((byte)(c >> 24));
        }
    }
    var z = new MemoryStream();
    using (var zs = new ZLibStream(z, CompressionLevel.SmallestSize, leaveOpen: true)) { raw.Position = 0; raw.CopyTo(zs); }

    var png = new MemoryStream();
    png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    void Chunk(string type, byte[] data)
    {
        var body = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        png.Write(BigEndian(data.Length));
        png.Write(body);
        png.Write(BigEndian((int)~Crc(body)));
    }
    var ihdr = BigEndian(size).Concat(BigEndian(size)).Concat(new byte[] { 8, 6, 0, 0, 0 }).ToArray();   // 8-bit RGBA
    Chunk("IHDR", ihdr);
    Chunk("IDAT", z.ToArray());
    Chunk("IEND", Array.Empty<byte>());
    return png.ToArray();
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
