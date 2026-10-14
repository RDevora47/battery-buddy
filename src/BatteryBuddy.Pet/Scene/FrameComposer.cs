using BatteryBuddy.Devices;

namespace BatteryBuddy.Pet.Scene;

public enum HitTarget { None, Body, Device, Channel }

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
    public HitTarget HitTest(int x, int y, out DeviceReading? device) => HitTest(x, y, out device, out _);

    /// <summary>As above; for a channel bubble, its number in <paramref name="channel"/>.</summary>
    public HitTarget HitTest(int x, int y, out DeviceReading? device, out int channel)
    {
        device = null;
        channel = 0;
        if (x < 0 || y < 0 || x >= Width || y >= Height) return HitTarget.None;
        short owner = _owners[y * Width + x];
        if (owner < 0) return HitTarget.None;
        if (owner == FrameComposer.BodyOwner) return HitTarget.Body;
        if (owner >= FrameComposer.ChannelOwnerBase)
        {
            channel = owner - FrameComposer.ChannelOwnerBase;
            return HitTarget.Channel;
        }
        device = Placements[owner - 1].Device;
        return HitTarget.Device;
    }
}

public static class FrameComposer
{
    internal const short NoOwner = -1;
    internal const short BodyOwner = 0;
    // Channel bubble n is owned by ChannelOwnerBase + n, well past any device index.
    internal const short ChannelOwnerBase = 1000;
    const double GreyedFade = 0.7;
    // Each bud's bar sits this far left or right of where the pair's one bar would: side by side, 1px apart.
    const int BudBarShift = (BatteryBar.Width + 1) / 2;
    static readonly double[] TrailDots = { 0.3, 0.5, 0.7 };
    // The place the earbuds go: the gills on the axolotl, the ears on every other pet.
    const string EarPlace = "gills";

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
        int w = layout.FrameWidth * res, h = layout.CanvasHeight * res;
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

        void DrawBar(int? percent, bool stale, int ox, int oy, short owner)
        {
            int filled = BatteryBar.FilledSegments(percent);
            uint color = BatteryBar.ColorFor(percent, stale);
            for (int s = 0; s < BatteryBar.SegmentCount; s++)
                for (int y = 0; y < BatteryBar.Height; y++)
                    for (int x = 0; x < 2; x++)
                        PlotArt(ox + s * 3 + x, oy + y, s < filled ? color : BatteryBar.Empty, owner);
        }

        // Mini battery at art x, vertically centred on a sprite whose top is at art y and art height h.
        // Mixels are half an art pixel (a whole one if the frame has no @2x resolution).
        void DrawGauge(int? percent, bool stale, int ax, int ay, int h, short owner)
        {
            int cell = Math.Max(1, res / 2);
            int lit = BatteryGauge.LitCells(percent);
            uint fill = BatteryBar.ColorFor(percent, stale);
            int top = ay * res + (h * res - BatteryGauge.MixelHeight * cell) / 2;
            for (int y = 0; y < BatteryGauge.MixelHeight; y++)
                for (int x = 0; x < BatteryGauge.MixelWidth; x++)
                    if (BatteryGauge.MixelColor(x, y, lit, fill) is var color and not 0)
                        Plot(ax * res + x * cell, top + y * cell, cell, color, owner);
        }

        // Ring one sprite pixel wide just outside the sprite's opaque pixels, covering only the remaining
        // share of it: clockwise from 12 o'clock, so it drains anticlockwise from the top. Unknown shows it all.
        void DrawOutline(Sprite sprite, int ox, int oy, uint color, int? percent, short owner)
        {
            var ring = new List<(int X, int Y)>();
            for (int y = -1; y <= sprite.Height; y++)
                for (int x = -1; x <= sprite.Width; x++)
                    if (!Opaque(sprite, x, y) &&
                        (Opaque(sprite, x - 1, y) || Opaque(sprite, x + 1, y) || Opaque(sprite, x, y - 1) || Opaque(sprite, x, y + 1)))
                        ring.Add((x, y));
            int shown = percent is int p ? (int)Math.Ceiling(ring.Count * Math.Clamp(p, 0, 100) / 100.0) : ring.Count;
            foreach (var (x, y) in ring.OrderBy(pt => ClockwiseFromTop(sprite, pt.X, pt.Y)).Take(shown))
                PlotSprite(sprite, ox, oy, x, y, color, owner);
        }

        // Channel bubbles over everything: trail dots first, then each disc and its digit. A bubble owns its whole
        // disc, even a hollow one, so hovering and clicking find it.
        void DrawPicker(PickerDrawing picker)
        {
            var disc = sprites["channel_bubble"];
            int cell = res / disc.Density;
            if (picker.Trail)
            {
                var dot = sprites["channel_trail"];
                double fx = (picker.Mouse.X + sprites["mouse"].ArtWidth / 2.0) * res, fy = picker.Mouse.Y * res;
                foreach (var bubble in picker.Bubbles)
                {
                    double tx = (bubble.At.X + ChannelArc.BubbleSize / 2.0) * res, ty = (bubble.At.Y + ChannelArc.BubbleSize) * res;
                    foreach (double t in TrailDots)
                        BlitAt(dot, (int)Math.Round(fx + (tx - fx) * t), (int)Math.Round(fy + (ty - fy) * t), NoOwner,
                            bubble.Look == BubbleLook.Pickable ? 0 : GreyedFade);
                }
            }
            foreach (var bubble in picker.Bubbles)
            {
                short owner = (short)(ChannelOwnerBase + bubble.Number);
                int x = bubble.At.X, y = bubble.At.Y - (bubble.Hovered && bubble.Look == BubbleLook.Pickable ? 1 : 0);
                double fade = bubble.Look == BubbleLook.Pickable ? 0 : GreyedFade;
                if (bubble.Look == BubbleLook.Empty)
                {
                    Claim(disc, x, y, owner);
                    Blit(sprites["channel_empty"], x, y, owner);
                }
                else Blit(disc, x, y, owner, fade);
                if (sprites.TryGetValue($"channel_digit{bubble.Number}", out var digit))
                    BlitAt(digit, x * res + (disc.Width - digit.Width) / 2 * cell, y * res + cell, owner, fade);
            }
        }

        // Owns a sprite's opaque pixels and the one-pixel ring around them (where its battery outline goes), without drawing.
        void ClaimWithRing(Sprite sprite, int ox, int oy, short owner)
        {
            int cell = res / sprite.Density;
            for (int y = -1; y <= sprite.Height; y++)
                for (int x = -1; x <= sprite.Width; x++)
                {
                    if (!(Opaque(sprite, x, y) || Opaque(sprite, x - 1, y) || Opaque(sprite, x + 1, y) ||
                          Opaque(sprite, x, y - 1) || Opaque(sprite, x, y + 1))) continue;
                    for (int py = Math.Max(oy * res + y * cell, 0); py < Math.Min(oy * res + (y + 1) * cell, h); py++)
                        for (int px = Math.Max(ox * res + x * cell, 0); px < Math.Min(ox * res + (x + 1) * cell, w); px++)
                            owners[py * w + px] = owner;
                }
        }

        // Owns a sprite's opaque pixels without drawing them.
        void Claim(Sprite sprite, int ox, int oy, short owner)
        {
            int cell = res / sprite.Density;
            for (int y = 0; y < sprite.Height; y++)
                for (int x = 0; x < sprite.Width; x++)
                {
                    if (sprite.Pixels[y * sprite.Width + x] == 0) continue;
                    for (int py = Math.Max(oy * res + y * cell, 0); py < Math.Min(oy * res + (y + 1) * cell, h); py++)
                        for (int px = Math.Max(ox * res + x * cell, 0); px < Math.Min(ox * res + (x + 1) * cell, w); px++)
                            owners[py * w + px] = owner;
                }
        }

        void DrawDevice(int index)
        {
            var placement = placements[index];
            var place = layout.Places[placement.PlaceName];
            var sprite = sprites[placement.SpriteName];
            int dy = (place.FollowsBody ? spec.BodyDy : 0) + (place.FollowsClick ? spec.ClickDy : 0);
            int bodyDx = place.FollowsBody ? spec.BodyDx : 0;
            short owner = (short)(index + 1);
            int? battery = placement.Device.EffectiveBattery;
            // Only a device running out shakes; one on its charger is being looked after.
            int dx = bodyDx + (placement.Device.UnchargedBattery <= BatteryBar.CriticalAtOrBelow ? spec.CriticalDx : 0);

            // A device without a battery gets no ring, bar, gauge or status icon: just the device.
            if (placement.Device.NoBattery)
            {
                foreach (var point in place.Points)
                    Blit(sprite, point.X + dx, point.Y + dy, owner);
                return;
            }

            if (placement.Device.Detail is { } buds && place.Points.Count == 2)
            {
                DrawBuds(buds, placement.Device.BatteryStale, place, sprite, dy, bodyDx, owner);
                return;
            }

            if (style == BatteryStyle.Outline)
                foreach (var point in place.Points)
                    DrawOutline(sprite, point.X + dx, point.Y + dy, BatteryBar.ColorFor(battery, placement.Device.BatteryStale), battery, owner);
            foreach (var point in place.Points)
                Blit(sprite, point.X + dx, point.Y + dy, owner);
            if (style == BatteryStyle.Bar)
                DrawBar(battery, placement.Device.BatteryStale, place.Bar.X + bodyDx, place.Bar.Y + dy, owner);

            var last = place.Points[^1];
            if (style == BatteryStyle.Gauge)
            {
                // On the place's icon side (default: right, flipping left at the canvas edge) with a 1px gap,
                // a charging bolt past it. A full battery lights every cell, so there's no full icon.
                bool left = place.Icons == Side.Left ||
                    place.Icons is null && last.X + sprite.ArtWidth + 1 + BatteryGauge.ArtWidth > layout.CanvasWidth;
                int gx = left ? place.Points[0].X - BatteryGauge.ArtWidth - 1 : last.X + sprite.ArtWidth + 1;
                DrawGauge(battery, placement.Device.BatteryStale, gx, last.Y + dy, sprite.ArtHeight, owner);
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
                DrawBadge(sprites[status], sprite, last.X, last.Y + dy, owner);
        }

        // A small badge at the sprite's top-right, above it: it stays over the device's own column,
        // so it never reaches a neighbouring device or the bar below.
        void DrawBadge(Sprite icon, Sprite sprite, int x, int y, short owner)
        {
            int sx = Math.Clamp(x + sprite.ArtWidth - icon.ArtWidth, 0, layout.CanvasWidth - icon.ArtWidth);
            int sy = y - icon.ArtHeight;
            if (sy < 0) sy = y;   // no room above (top row): overlap the sprite's own corner
            Blit(icon, sx, sy, owner);
        }

        // Galaxy Buds with detail: each bud wears its own ring, bar or mini battery, left bud on the place's first
        // point, right on its second (as they read in the bubble). A bud in the case charges; a critical one out of
        // it shakes. The bars sit side by side where the pair's one bar would; the mini batteries go outward.
        void DrawBuds(BudsDetail buds, bool stale, Place place, Sprite sprite, int dy, int bodyDx, short owner)
        {
            var each = new[]
            {
                (At: place.Points[0], Level: buds.Left, InCase: buds.LeftInCase, Side: Side.Left),
                (At: place.Points[1], Level: buds.Right, InCase: buds.RightInCase, Side: Side.Right),
            };
            foreach (var bud in each)
            {
                int x = bud.At.X + bodyDx, y = bud.At.Y + dy;
                int dx = !bud.InCase && bud.Level <= BatteryBar.CriticalAtOrBelow ? spec.CriticalDx : 0;
                bool charging = bud.InCase && bud.Level != 100;
                if (style == BatteryStyle.Outline)
                    DrawOutline(sprite, x + dx, y, BatteryBar.ColorFor(bud.Level, stale), bud.Level, owner);
                Blit(sprite, x + dx, y, owner);

                if (style == BatteryStyle.Gauge)
                {
                    bool left = bud.Side == Side.Left;
                    int gx = left ? x - BatteryGauge.ArtWidth - 1 : x + sprite.ArtWidth + 1;
                    DrawGauge(bud.Level, stale, gx, y, sprite.ArtHeight, owner);
                    if (charging)
                    {
                        var bolt = sprites["bolt"];
                        BlitCentred(bolt, left ? gx - bolt.ArtWidth - 1 : gx + BatteryGauge.ArtWidth + 1, y, sprite.ArtHeight, owner);
                    }
                    continue;
                }
                if ((bud.Level == 100 ? "full" : charging ? "bolt" : null) is string status)
                    DrawBadge(sprites[status], sprite, x, y, owner);
                if (style == BatteryStyle.Bar)
                    DrawBar(bud.Level, stale, place.Bar.X + bodyDx + (bud.Side == Side.Left ? -BudBarShift : BudBarShift),
                        place.Bar.Y + dy, owner);
            }
        }

        bool reaching = spec.RightPawOnMouse && layout.MousePaw is not null && !layout.Perched;
        // A skin with a reaching limb clicks with it instead, so its right paw stays on the keys, at rest.
        bool limb = reaching && layout.Reach is not null;

        void DrawPet()
        {
            int x = layout.Body.X + spec.BodyDx, y = layout.Body.Y + spec.BodyDy;
            if (layout.Tail is PixelPoint tail && spec.Tail is not null)
                Blit(sprites[limb ? "tail_reach" : spec.Tail], tail.X + spec.BodyDx, tail.Y + spec.BodyDy, BodyOwner, spec.Fade);
            // A pet that wags with its whole body (Gumersindo) has a body frame per wag pose: "tail_wag1" draws "body_wag1".
            var wagBody = spec.Tail?.StartsWith("tail_") == true ? "body_" + spec.Tail["tail_".Length..] : null;
            var body = wagBody is not null && sprites.ContainsKey(wagBody) ? wagBody : spec.BodySprite;
            Blit(sprites[body], x, y, BodyOwner, spec.Fade);
            if (spec.Gills is not null)
                Blit(sprites[spec.Gills], x, y, BodyOwner, spec.Fade);
        }

        void DrawDeskAndKeyboard()
        {
            if (layout.Desk is PixelPoint desk)
                Blit(sprites["desk"], desk.X, desk.Y, BodyOwner);
            for (int i = 0; i < placements.Count; i++)
                if (placements[i].PlaceName == "seat") DrawDevice(i);
        }

        // Back to front: the tail behind the pet, pet, the desk in front of it, the keyboard on the desk,
        // the paws tapping it, then everything the pet holds or wears. A perched pet stands in front of the
        // desk on the keyboard instead; its paws are its feet, and they hop with it rather than tapping
        // (unless it Taps: then its feet tap the keys like paws, and its legs, drawn behind the body, run up
        // under it so a tapping foot never comes loose). Either way a perched pet's feet stay on the keys
        // when it uses the mouse.
        // A sitting pet with a PawLift rests its paws that many rows higher; a tap still lands on the keys.
        int PawDrop(Paw paw)
        {
            int drop = layout.Hops || limb && paw == Paw.Right || layout.Perched && paw == Paw.Right && spec.ClickDy > 0
                ? 0 : spec.PawDy(paw);
            return drop > 0 || layout.Perched ? drop : -layout.PawLift;
        }
        void DrawPaws()
        {
            if (layout.Paws is null) return;
            foreach (var (paw, point) in layout.Paws)
                if (!(reaching && paw == Paw.Right && !limb))
                    Blit(sprites["paw"], point.X + spec.BodyDx, point.Y + spec.BodyDy + PawDrop(paw), BodyOwner, spec.Fade);
        }

        if (layout.Perched)
        {
            DrawDeskAndKeyboard();
            if (layout.Taps) DrawPaws();
            DrawPet();
            if (!layout.Taps) DrawPaws();
        }
        else
        {
            DrawPet();
            DrawDeskAndKeyboard();
            DrawPaws();
        }
        for (int i = 0; i < placements.Count; i++)
            if (placements[i].PlaceName is not ("seat" or EarPlace)) DrawDevice(i);
        if (reaching)   // on the mouse, which sits on the desk and doesn't bob
        {
            // The limb stays put on a click: its paw presses the button, the tip stretching down.
            if (layout.Reach is PixelPoint reach)
                Blit(sprites[spec.ReachStep > 0 ? $"reach{spec.ReachStep}" : "reach"], reach.X, reach.Y, BodyOwner, spec.Fade);
            if (!limb || spec.ReachStep == 0)
                Blit(sprites["paw"], layout.MousePaw!.Value.X, layout.MousePaw.Value.Y + spec.PawRightDy, BodyOwner, spec.Fade);
        }
        // Clicking the mouse is itself a click, so the pet presses it (dip, paw or limb on it) before the click is
        // hit-tested: the mouse keeps its resting footprint so none of that steals the click.
        for (int i = 0; i < placements.Count; i++)
        {
            var place = layout.Places[placements[i].PlaceName];
            if (!place.FollowsClick) continue;
            foreach (var point in place.Points)
                ClaimWithRing(sprites[placements[i].SpriteName], point.X, point.Y, (short)(i + 1));
        }
        foreach (var overlay in spec.Overlays)
            Blit(sprites[overlay.Sprite], overlay.X, overlay.Y, BodyOwner);
        // Earbuds sit in the ears, which a hat often covers; they stay in view over it.
        for (int i = 0; i < placements.Count; i++)
            if (placements[i].PlaceName == EarPlace) DrawDevice(i);
        if (spec.Picker is { } picker) DrawPicker(picker);

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

    // Angle in [0, 2pi) of sprite pixel (x, y)'s centre around the sprite's centre: 0 at 12 o'clock, growing
    // clockwise on screen (y points down).
    static double ClockwiseFromTop(Sprite sprite, int x, int y)
    {
        double angle = Math.Atan2(x + 0.5 - sprite.Width / 2.0, sprite.Height / 2.0 - (y + 0.5));
        return angle < 0 ? angle + 2 * Math.PI : angle;
    }

    static bool Opaque(Sprite sprite, int x, int y) =>
        x >= 0 && y >= 0 && x < sprite.Width && y < sprite.Height && sprite.Pixels[y * sprite.Width + x] != 0;
}
