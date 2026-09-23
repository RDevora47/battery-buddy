using BatteryBuddy.Core.Devices;

namespace BatteryBuddy.Core.Scene;

public enum HitTarget { None, Body, Device }

public sealed class ComposedFrame
{
    readonly short[] _owners;

    internal ComposedFrame(int width, int height, uint[] pixels, short[] owners, IReadOnlyList<DevicePlacement> placements)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
        _owners = owners;
        Placements = placements;
    }

    public int Width { get; }
    public int Height { get; }
    public uint[] Pixels { get; }
    public IReadOnlyList<DevicePlacement> Placements { get; }

    public HitTarget HitTest(int x, int y, out DeviceReading? device)
    {
        device = null;
        if (x < 0 || y < 0 || x >= Width || y >= Height) return HitTarget.None;
        short owner = _owners[y * Width + x];
        if (owner < 0) return HitTarget.None;
        if (owner == FrameComposer.BodyOwner) return HitTarget.Body;
        device = Placements[owner - 1].Device;
        return HitTarget.Device;
    }
}

public static class FrameComposer
{
    internal const short NoOwner = -1;
    internal const short BodyOwner = 0;

    public static ComposedFrame Compose(
        SkinLayout layout,
        IReadOnlyDictionary<string, Sprite> sprites,
        IReadOnlyList<DevicePlacement> placements,
        FrameSpec spec)
    {
        int w = layout.CanvasWidth, h = layout.CanvasHeight;
        var pixels = new uint[w * h];
        var owners = new short[w * h];
        Array.Fill(owners, NoOwner);

        void Plot(int x, int y, uint color, short owner)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            pixels[y * w + x] = color;
            owners[y * w + x] = owner;
        }

        void Blit(Sprite sprite, int ox, int oy, short owner)
        {
            for (int y = 0; y < sprite.Height; y++)
                for (int x = 0; x < sprite.Width; x++)
                {
                    uint color = sprite.Pixels[y * sprite.Width + x];
                    if (color != 0) Plot(ox + x, oy + y, color, owner);
                }
        }

        void DrawBar(int? percent, int ox, int oy, short owner)
        {
            int filled = BatteryBar.FilledSegments(percent);
            uint color = BatteryBar.ColorFor(percent);
            for (int s = 0; s < BatteryBar.SegmentCount; s++)
                for (int y = 0; y < BatteryBar.Height; y++)
                    for (int x = 0; x < 2; x++)
                        Plot(ox + s * 3 + x, oy + y, s < filled ? color : BatteryBar.Empty, owner);
        }

        void DrawDevice(int index)
        {
            var placement = placements[index];
            var place = layout.Places[placement.PlaceName];
            int dy = place.FollowsBody ? spec.BodyDy : 0;
            short owner = (short)(index + 1);
            int? battery = placement.Device.EffectiveBattery;
            bool critical = battery <= BatteryBar.CriticalAtOrBelow;

            if (!critical || spec.CriticalVisible)
                foreach (var point in place.Points)
                    Blit(sprites[placement.SpriteName], point.X, point.Y + dy, owner);
            DrawBar(battery, place.Bar.X, place.Bar.Y + dy, owner);
        }

        for (int i = 0; i < placements.Count; i++)
            if (placements[i].PlaceName == "seat") DrawDevice(i);
        Blit(sprites[spec.BodySprite], layout.Body.X, layout.Body.Y + spec.BodyDy, BodyOwner);
        for (int i = 0; i < placements.Count; i++)
            if (placements[i].PlaceName != "seat") DrawDevice(i);
        foreach (var overlay in spec.Overlays)
            Blit(sprites[overlay.Sprite], overlay.X, overlay.Y, BodyOwner);

        return new ComposedFrame(w, h, pixels, owners, placements);
    }
}
