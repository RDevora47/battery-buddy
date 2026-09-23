using BatteryBuddy.Devices;

namespace BatteryBuddy.Pet.Scene;

public enum HitTarget { None, Body, Device }

public sealed class ComposedFrame
{
    readonly short[] _owners;

    internal ComposedFrame(int width, int height, int resolution, uint[] pixels, short[] owners, IReadOnlyList<DevicePlacement> placements)
    {
        Width = width;
        Height = height;
        Resolution = resolution;
        Pixels = pixels;
        _owners = owners;
        Placements = placements;
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Frame pixels per art pixel per axis: the highest sprite density in the skin (1 without "@2x" sprites).</summary>
    public int Resolution { get; }

    /// <summary>Width x Height frame pixels; the canvas scaled up by Resolution.</summary>
    public uint[] Pixels { get; }
    public IReadOnlyList<DevicePlacement> Placements { get; }

    /// <summary>(x, y) in frame pixels, not art pixels.</summary>
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

    /// <summary>Frame pixels per art pixel: the highest sprite density, which every other density must divide.</summary>
    public static int ResolutionOf(IReadOnlyDictionary<string, Sprite> sprites)
    {
        int res = sprites.Values.Max(s => s.Density);
        if (sprites.Values.FirstOrDefault(s => res % s.Density != 0) is { } odd)
            throw new ArgumentException($"sprite '{odd.Name}' @{odd.Density}x doesn't divide the frame's {res}x resolution");
        return res;
    }

    public static ComposedFrame Compose(
        SkinLayout layout,
        IReadOnlyDictionary<string, Sprite> sprites,
        IReadOnlyList<DevicePlacement> placements,
        FrameSpec spec,
        BatteryStyle style = BatteryStyle.Outline)
    {
        int res = ResolutionOf(sprites);
        int w = layout.CanvasWidth * res, h = layout.CanvasHeight * res;
        var pixels = new uint[w * h];
        var owners = new short[w * h];
        Array.Fill(owners, NoOwner);

        // Fills a size x size block of frame pixels.
        void Plot(int x, int y, int size, uint color, short owner)
        {
            for (int py = Math.Max(y, 0); py < Math.Min(y + size, h); py++)
                for (int px = Math.Max(x, 0); px < Math.Min(x + size, w); px++)
                {
                    pixels[py * w + px] = color;
                    owners[py * w + px] = owner;
                }
        }

        void PlotArt(int x, int y, uint color, short owner) => Plot(x * res, y * res, res, color, owner);

        // Sprite pixel (x, y) of a sprite whose top-left sits at art pixel (ox, oy).
        void PlotSprite(Sprite sprite, int ox, int oy, int x, int y, uint color, short owner)
        {
            int cell = res / sprite.Density;
            Plot(ox * res + x * cell, oy * res + y * cell, cell, color, owner);
        }

        void Blit(Sprite sprite, int ox, int oy, short owner, double fade = 0) => BlitAt(sprite, ox * res, oy * res, owner, fade);

        // Top-left at frame pixel (fx, fy), so @2x sprites can sit on half art pixels.
        void BlitAt(Sprite sprite, int fx, int fy, short owner, double fade = 0)
        {
            int cell = res / sprite.Density;
            for (int y = 0; y < sprite.Height; y++)
                for (int x = 0; x < sprite.Width; x++)
                {
                    uint color = sprite.Pixels[y * sprite.Width + x];
                    if (color != 0) Plot(fx + x * cell, fy + y * cell, cell, fade > 0 ? Desaturate(color, fade) : color, owner);
                }
        }

        // At art x, vertically centred on a sprite whose top is at art y and art height h.
        void BlitCentred(Sprite sprite, int ax, int ay, int h, short owner) =>
            BlitAt(sprite, ax * res, ay * res + (h * res - sprite.Height * (res / sprite.Density)) / 2, owner);

        void DrawBar(int? percent, int ox, int oy, short owner)
        {
            int filled = BatteryBar.FilledSegments(percent);
            uint color = BatteryBar.ColorFor(percent);
            for (int s = 0; s < BatteryBar.SegmentCount; s++)
                for (int y = 0; y < BatteryBar.Height; y++)
                    for (int x = 0; x < 2; x++)
                        PlotArt(ox + s * 3 + x, oy + y, s < filled ? color : BatteryBar.Empty, owner);
        }

        // Mini battery at art x, vertically centred on a sprite whose top is at art y and art height h.
        // Mixels are half an art pixel (a whole one if the frame has no @2x resolution).
        void DrawGauge(DeviceReading device, int ax, int ay, int h, short owner)
        {
            int cell = Math.Max(1, res / 2);
            int lit = BatteryGauge.LitCells(device.EffectiveBattery);
            uint fill = BatteryBar.ColorFor(device.EffectiveBattery);
            int top = ay * res + (h * res - BatteryGauge.MixelHeight * cell) / 2;
            for (int y = 0; y < BatteryGauge.MixelHeight; y++)
                for (int x = 0; x < BatteryGauge.MixelWidth; x++)
                    if (BatteryGauge.MixelColor(x, y, lit, fill) is var color and not 0)
                        Plot(ax * res + x * cell, top + y * cell, cell, color, owner);
        }

        // Ring one sprite pixel wide just outside the sprite's opaque pixels.
        void DrawOutline(Sprite sprite, int ox, int oy, uint color, short owner)
        {
            for (int y = -1; y <= sprite.Height; y++)
                for (int x = -1; x <= sprite.Width; x++)
                    if (!Opaque(sprite, x, y) &&
                        (Opaque(sprite, x - 1, y) || Opaque(sprite, x + 1, y) || Opaque(sprite, x, y - 1) || Opaque(sprite, x, y + 1)))
                        PlotSprite(sprite, ox, oy, x, y, color, owner);
        }

        void DrawDevice(int index)
        {
            var placement = placements[index];
            var place = layout.Places[placement.PlaceName];
            var sprite = sprites[placement.SpriteName];
            int dy = (place.FollowsBody ? spec.BodyDy : 0) + (place.FollowsClick ? spec.ClickDy : 0);
            short owner = (short)(index + 1);
            int? battery = placement.Device.EffectiveBattery;
            int dx = battery <= BatteryBar.CriticalAtOrBelow ? spec.CriticalDx : 0;

            if (style == BatteryStyle.Outline)
                foreach (var point in place.Points)
                    DrawOutline(sprite, point.X + dx, point.Y + dy, BatteryBar.ColorFor(battery), owner);
            foreach (var point in place.Points)
                Blit(sprite, point.X + dx, point.Y + dy, owner);
            if (style == BatteryStyle.Bar)
                DrawBar(battery, place.Bar.X, place.Bar.Y + dy, owner);

            var last = place.Points[^1];
            if (style == BatteryStyle.Gauge)
            {
                // On the place's icon side (default: right, flipping left at the canvas edge) with a 1px gap,
                // a charging bolt past it. A full battery lights every cell, so there's no full icon.
                bool left = place.Icons == Side.Left ||
                    place.Icons is null && last.X + sprite.ArtWidth + 1 + BatteryGauge.ArtWidth > layout.CanvasWidth;
                int gx = left ? place.Points[0].X - BatteryGauge.ArtWidth - 1 : last.X + sprite.ArtWidth + 1;
                DrawGauge(placement.Device, gx, last.Y + dy, sprite.ArtHeight, owner);
                if (placement.Device.IsCharging && battery != 100)
                {
                    var bolt = sprites["bolt"];
                    int bx = left ? gx - bolt.ArtWidth - 1 : gx + BatteryGauge.ArtWidth + 1;
                    BlitCentred(bolt, bx, last.Y + dy, sprite.ArtHeight, owner);
                }
                return;
            }

            string? status = battery == 100 ? "full" : placement.Device.IsCharging ? "bolt" : null;
            if (status is not null && place.Icons is Side side)
            {
                // Beside the device, clear of the outline ring.
                var icon = sprites[status];
                int gap = style == BatteryStyle.Outline ? 2 : 1;
                int ix = side == Side.Left ? place.Points[0].X - gap - icon.ArtWidth : last.X + sprite.ArtWidth + gap;
                BlitCentred(icon, ix, last.Y + dy, sprite.ArtHeight, owner);
            }
            else if (status is not null)
            {
                // A small badge at the sprite's top-right, above it: it stays over the device's own column,
                // so it never reaches a neighbouring device or the bar below.
                var icon = sprites[status];
                int sx = Math.Clamp(last.X + sprite.ArtWidth - icon.ArtWidth, 0, layout.CanvasWidth - icon.ArtWidth);
                int sy = last.Y + dy - icon.ArtHeight;
                if (sy < 0) sy = last.Y + dy;   // no room above (top row): overlap the sprite's own corner
                Blit(icon, sx, sy, owner);
            }
        }

        // Back to front: pet, the desk in front of it, the keyboard on the desk, the paws tapping it,
        // then everything the pet holds or wears.
        Blit(sprites[spec.BodySprite], layout.Body.X, layout.Body.Y + spec.BodyDy, BodyOwner, spec.Fade);
        if (spec.Gills is not null)
            Blit(sprites[spec.Gills], layout.Body.X, layout.Body.Y + spec.BodyDy, BodyOwner, spec.Fade);
        if (layout.Desk is PixelPoint desk)
            Blit(sprites["desk"], desk.X, desk.Y, BodyOwner);
        for (int i = 0; i < placements.Count; i++)
            if (placements[i].PlaceName == "seat") DrawDevice(i);
        bool reaching = spec.RightPawOnMouse && layout.MousePaw is not null;
        if (layout.Paws is not null)
            foreach (var (paw, point) in layout.Paws)
                if (!(reaching && paw == Paw.Right))
                    Blit(sprites["paw"], point.X, point.Y + spec.BodyDy + spec.PawDy(paw), BodyOwner, spec.Fade);
        for (int i = 0; i < placements.Count; i++)
            if (placements[i].PlaceName != "seat") DrawDevice(i);
        if (reaching)   // on the mouse, which sits on the desk and doesn't bob
            Blit(sprites["paw"], layout.MousePaw!.Value.X, layout.MousePaw.Value.Y + spec.PawRightDy, BodyOwner, spec.Fade);
        foreach (var overlay in spec.Overlays)
            Blit(sprites[overlay.Sprite], overlay.X, overlay.Y, BodyOwner);

        return new ComposedFrame(w, h, res, pixels, owners, placements);
    }

    // Moves the color toward its own luminance grey; alpha is kept.
    static uint Desaturate(uint argb, double amount)
    {
        int r = (int)(argb >> 16 & 0xFF), g = (int)(argb >> 8 & 0xFF), b = (int)(argb & 0xFF);
        double grey = 0.3 * r + 0.59 * g + 0.11 * b;
        uint Mix(int c) => (uint)Math.Round(c + (grey - c) * amount);
        return argb & 0xFF000000u | Mix(r) << 16 | Mix(g) << 8 | Mix(b);
    }

    static bool Opaque(Sprite sprite, int x, int y) =>
        x >= 0 && y >= 0 && x < sprite.Width && y < sprite.Height && sprite.Pixels[y * sprite.Width + x] != 0;
}
