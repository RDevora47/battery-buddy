using BatteryBuddy.Pet.Scene;

namespace BatteryBuddy.Tests;

public class IconCanvasTests
{
    static Sprite Solid(int w, int h, int density = 1) =>
        new("s", w, h, Enumerable.Repeat(0xFF000000u, w * h).ToArray(), density);

    [Fact]
    public void Small_sprite_is_centred_on_a_32px_square()
    {
        var (size, pixels) = IconCanvas.Square(Solid(4, 2));
        Assert.Equal(32, size);
        Assert.Equal(0xFF000000u, pixels[15 * 32 + 14]);
        Assert.Equal(0u, pixels[15 * 32 + 13]);
        Assert.Equal(0u, pixels[14 * 32 + 14]);
    }

    [Fact]
    public void Sprite_wider_than_32px_grows_the_square_so_nothing_is_cut_off()
    {
        var (size, pixels) = IconCanvas.Square(Solid(64, 36, density: 2));
        Assert.Equal(64, size);
        Assert.Equal(64 * 36, pixels.Count(p => p != 0));
        Assert.Equal(0xFF000000u, pixels[14 * 64]);   // centred vertically: rows 14..49
        Assert.Equal(0u, pixels[13 * 64]);
    }

    [Theory, MemberData(nameof(SkinTests.Skins), MemberType = typeof(SkinTests))]
    public void Tray_icon_fits(string skin)
    {
        var sprites = TestSkin.Load(skin).Sprites;
        var (size, pixels) = IconCanvas.Square(sprites["body_idle"]);
        Assert.Equal(size * size, pixels.Length);
        Assert.True(size >= sprites["body_idle"].Width && size >= sprites["body_idle"].Height);
    }
}
