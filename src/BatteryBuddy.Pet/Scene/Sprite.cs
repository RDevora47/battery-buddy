namespace BatteryBuddy.Pet.Scene;

/// <summary>
/// Pixels are ARGB (0xAARRGGBB); 0 is transparent. Width/Height count the sprite's own pixels;
/// Density is how many of them fit in one art pixel per axis ("@2x" sprites are drawn with half-size pixels).
/// </summary>
public sealed record Sprite(string Name, int Width, int Height, uint[] Pixels, int Density = 1)
{
    /// <summary>Footprint in art (canvas) pixels.</summary>
    public int ArtWidth => Width / Density;
    public int ArtHeight => Height / Density;
}
