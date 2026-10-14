namespace BatteryBuddy.Pet.Scene;

/// <summary>
/// The vanishing trick's smoke bomb is bigger than most pets' canvases, so it isn't drawn into the pet's frame (which would
/// clip it) but on a layer of its own over it, in the window's room around the pet. One size for every pet.
/// </summary>
public static class SmokeLayer
{
    /// <summary>How far, in art pixels, the cloud's bottom sits below the canvas: it billows up from the desk.</summary>
    public const int BelowCanvas = 4;

    /// <summary>Where the smoke sprite's top-left goes, in frame pixels from the pet frame's top-left: bottom-centred on the canvas.</summary>
    public static (int X, int Y) Offset(SkinLayout layout, Sprite smoke, int resolution)
    {
        int cell = resolution / smoke.Density;
        return ((layout.CanvasWidth * resolution - smoke.Width * cell) / 2,
                (layout.CanvasHeight + BelowCanvas) * resolution - smoke.Height * cell);
    }

    /// <summary>The sprite's pixels at the frame's resolution (each sprite pixel a cell x cell block).</summary>
    public static uint[] Pixels(Sprite smoke, int resolution)
    {
        int cell = resolution / smoke.Density, w = smoke.Width * cell;
        var pixels = new uint[w * smoke.Height * cell];
        for (int y = 0; y < smoke.Height * cell; y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = smoke.Pixels[y / cell * smoke.Width + x / cell];
        return pixels;
    }
}
