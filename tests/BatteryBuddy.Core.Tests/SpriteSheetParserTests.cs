using BatteryBuddy.Core.Scene;

namespace BatteryBuddy.Core.Tests;

public class SpriteSheetParserTests
{
    const string Sheet = """
        # test sheet
        palette
        k #102030
        sprite dot
        .k.
        kkk
        end
        """;

    [Fact]
    public void Parses_palette_and_pixels()
    {
        var dot = SpriteSheetParser.Parse(Sheet)["dot"];
        Assert.Equal((3, 2), (dot.Width, dot.Height));
        Assert.Equal(0u, dot.Pixels[0]);
        Assert.Equal(0xFF102030u, dot.Pixels[1]);
    }

    [Fact]
    public void Ragged_rows_are_rejected_with_sprite_name()
    {
        var ex = Assert.Throws<FormatException>(() => SpriteSheetParser.Parse("palette\nk #000000\nsprite bad\nkk\nk\nend"));
        Assert.Contains("bad", ex.Message);
    }

    [Fact]
    public void Unknown_color_is_rejected() =>
        Assert.Throws<FormatException>(() => SpriteSheetParser.Parse("palette\nk #000000\nsprite bad\nkx\nend"));

    [Fact]
    public void Missing_end_is_rejected() =>
        Assert.Throws<FormatException>(() => SpriteSheetParser.Parse("palette\nk #000000\nsprite bad\nkk"));
}
