// Renders the README's animated pictures into docs/images with the app's own animator and frame composer,
// so they always match the real pets. Run after changing a skin:  dotnet run --project tools/MakeShowcase
// Pass a folder to also write a PNG of every GIF's first frame there, for a quick look.
using BatteryBuddy.Devices;
using BatteryBuddy.Pet.Animation;
using BatteryBuddy.Pet.Scene;

const int Scale = 4;   // screen pixels per art pixel
var fps = PetAnimator.Fps;
var root = FindRoot();
var skins = Path.Combine(root, "src", "BatteryBuddy.App", "Skins");
var output = Path.Combine(root, "docs", "images");
var preview = args.FirstOrDefault();
Directory.CreateDirectory(output);

string[] pets = { "axolotl", "redpanda", "bunny", "panda", "parrot", "jellyfish", "choppa", "missy" };

// A typical desk: buds on the ears, a mouse and keyboard on the desk.
DeviceReading[] Desk(int buds = 85, int mouse = 90, int keyboard = 75) => new[]
{
    Device("Galaxy Buds3 Pro", DeviceKind.Earbuds, buds, detail: new BudsDetail(buds, buds + 4, 60, true, true)),
    Device("MX Master 3S", DeviceKind.Mouse, mouse),
    Device("MX Keys", DeviceKind.Keyboard, keyboard),
};

// Types for two seconds, bobs happily, clicks the mouse twice, then rests: 7 s.
void Working(PetAnimator pet, int frame, TimeSpan now)
{
    if (frame is >= 3 and <= 14 && frame % 4 != 3) pet.KeyTap(now);
    if (frame is 26 or 29) pet.Click(now);
}

// Every pet at work, one GIF each.
var working = new Dictionary<string, Clip>();
foreach (var pet in pets)
{
    working[pet] = Render(pet, Desk(), 42, Working);
    Save($"pet-{pet}", working[pet]);
}

// Moods and extras on Mochi, 3 s each, with the idle burst (bob, Zzz drift or shake) near the start.
Save("mood-happy", Render("axolotl", Desk(), 18));
Save("mood-sleepy", Render("axolotl", Desk(buds: 18), 18));
Save("mood-critical", Render("axolotl", Desk(buds: 60, mouse: 8), 18));
Save("mood-full-saiyan", Render("axolotl", Desk(100, 100, 100), 18));
Save("mood-full-strawhat", Render("redpanda", Desk(100, 100, 100), 18, hat: FullChargeHat.StrawHat));
Save("rescan", Render("axolotl", Desk(), 18, (pet, frame, now) =>
{
    if (frame == 0) pet.BeginSniff(now);
    if (frame == 8) pet.EndSniff(now);
}));

// The banner: all eight at work, two rows of four, each row bottom-aligned.
Save("banner", Grid(pets.Select(p => working[p]).ToList(), columns: 4, gap: 6 * Scale));
Console.WriteLine($"Wrote {output}");

Clip Render(string skin, DeviceReading[] devices, int frames, Action<PetAnimator, int, TimeSpan>? script = null,
    FullChargeHat hat = FullChargeHat.SaiyanHair)
{
    var layout = SkinLayout.Parse(File.ReadAllText(Path.Combine(skins, skin, "skin.json")));
    var sprites = SpriteSheetParser.Parse(File.ReadAllText(Path.Combine(skins, "common", "sprites.txt")),
        File.ReadAllText(Path.Combine(skins, skin, "sprites.txt")));
    // With a random of 0 the idle burst comes every 3 s; starting the clock 2.5 s early plays the first at 0.5 s.
    var pet = new PetAnimator(() => 0, TimeSpan.FromSeconds(-2.5), layout.Overlays["zzz"], layout.Overlays["sweat"],
        layout.Overlays["saiyan"], layout.Overlays["strawhat"], layout.Overlays["magnifier"])
    {
        Hat = hat,
        Hops = layout.Perched,
        ReachFrames = layout.Reach is null ? 0 : Enumerable.Range(1, 9).TakeWhile(n => sprites.ContainsKey($"reach{n}")).Count(),
        Mood = MoodCalculator.From(devices),
        HasCritical = devices.Any(d => d.EffectiveBattery <= BatteryBar.CriticalAtOrBelow),
        LowestBattery = MoodCalculator.Lowest(devices),
        SuperSaiyan = MoodCalculator.SuperSaiyan(devices),
    };
    var placements = SlotAssigner.Assign(devices);

    var composed = new List<ComposedFrame>();
    for (int i = 0; i < frames; i++)
    {
        var now = TimeSpan.FromTicks(TimeSpan.TicksPerSecond * i / fps);
        script?.Invoke(pet, i, now);
        composed.Add(FrameComposer.Compose(layout, sprites, placements, pet.FrameAt(now)));
    }
    // Upscaled so each art pixel is Scale x Scale, then cropped to what any frame draws, plus a small margin.
    int k = Scale / composed[0].Resolution;
    var clip = new Clip(composed[0].Width * k, composed[0].Height * k,
        composed.Select(f => Enlarge(f.Pixels, f.Width, f.Height, k)).ToList());
    return Crop(clip, Scale);
}

void Save(string name, Clip clip)
{
    File.WriteAllBytes(Path.Combine(output, name + ".gif"), Gif.Encode(clip.Width, clip.Height, clip.Frames, 100 / fps));
    if (preview is not null) Png.Write(Path.Combine(preview, name + ".png"), clip.Width, clip.Height, clip.Frames[0]);
}

static uint[] Enlarge(uint[] src, int w, int h, int k)
{
    var dst = new uint[w * k * h * k];
    for (int y = 0; y < h * k; y++)
        for (int x = 0; x < w * k; x++)
            dst[y * w * k + x] = src[y / k * w + x / k];
    return dst;
}

static Clip Crop(Clip clip, int margin)
{
    int left = clip.Width, top = clip.Height, right = -1, bottom = -1;
    foreach (var f in clip.Frames)
        for (int y = 0; y < clip.Height; y++)
            for (int x = 0; x < clip.Width; x++)
                if (f[y * clip.Width + x] != 0)
                    (left, top, right, bottom) = (Math.Min(left, x), Math.Min(top, y), Math.Max(right, x), Math.Max(bottom, y));
    int w = right - left + 1 + 2 * margin, h = bottom - top + 1 + 2 * margin;
    return new Clip(w, h, clip.Frames.Select(f =>
    {
        var dst = new uint[w * h];
        for (int y = top; y <= bottom; y++)
            Array.Copy(f, y * clip.Width + left, dst, (y - top + margin) * w + margin, right - left + 1);
        return dst;
    }).ToList());
}

// Clips side by side in rows, each clip centred in its column and standing on its row's baseline.
static Clip Grid(IReadOnlyList<Clip> clips, int columns, int gap)
{
    int cellW = clips.Max(c => c.Width);
    var rows = clips.Chunk(columns).ToList();
    var rowH = rows.Select(r => r.Max(c => c.Height)).ToList();
    int w = columns * cellW + (columns - 1) * gap, h = rowH.Sum() + (rows.Count - 1) * gap;
    int frames = clips.Max(c => c.Frames.Count);
    var result = new List<uint[]>();
    for (int f = 0; f < frames; f++)
    {
        var dst = new uint[w * h];
        int oy = 0;
        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < rows[r].Length; c++)
            {
                var clip = rows[r][c];
                var src = clip.Frames[f % clip.Frames.Count];
                int ox = c * (cellW + gap) + (cellW - clip.Width) / 2, top = oy + rowH[r] - clip.Height;
                for (int y = 0; y < clip.Height; y++)
                    Array.Copy(src, y * clip.Width, dst, (top + y) * w + ox, clip.Width);
            }
            oy += rowH[r] + gap;
        }
        result.Add(dst);
    }
    return new Clip(w, h, result);
}

static DeviceReading Device(string name, DeviceKind kind, int percent, bool charging = false, BudsDetail? detail = null) =>
    new(DeviceKey.ForName(name), name, kind, true, percent, detail, DateTimeOffset.UnixEpoch, "showcase",
        IsCharging: charging, ChargingKnown: true);

static string FindRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "BatteryBuddy.sln"))) return dir.FullName;
    throw new InvalidOperationException("BatteryBuddy.sln not found above " + AppContext.BaseDirectory);
}

sealed record Clip(int Width, int Height, IReadOnlyList<uint[]> Frames);
