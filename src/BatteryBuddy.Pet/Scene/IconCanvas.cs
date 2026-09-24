namespace BatteryBuddy.Pet.Scene;

public static class IconCanvas
{
    public const int MinSize = 32;

    /// <summary>
    /// The sprite's own pixels centred on a transparent square: 32x32, or larger when the sprite is,
    /// so a hi-res (@2x) sprite is never cut off. Windows scales the icon down to the tray's size.
    /// </summary>
    public static (int Size, uint[] Pixels) Square(Sprite sprite)
    {
        int size = Math.Max(MinSize, Math.Max(sprite.Width, sprite.Height));
        var pixels = new uint[size * size];
        int ox = (size - sprite.Width) / 2, oy = (size - sprite.Height) / 2;
        for (int y = 0; y < sprite.Height; y++)
            Array.Copy(sprite.Pixels, y * sprite.Width, pixels, (y + oy) * size + ox, sprite.Width);
        return (size, pixels);
    }
}
