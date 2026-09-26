using System.IO;
using BatteryBuddy.Pet.Scene;

namespace BatteryBuddy.App;

static class SkinLoader
{
    public const string Default = "axolotl";

    /// <summary>The pets to choose from: skin folder and menu name.</summary>
    public static readonly (string Skin, string Name)[] Pets =
    {
        ("axolotl", "Mochi"),
        ("redpanda", "Maple"),
        ("bunny", "Bebonio"),
        ("panda", "Bao"),
        ("parrot", "Mango"),
        ("jellyfish", "Boba"),
        ("choppa", "Choppa"),
        ("missy", "Missy"),
        ("ragdoll", "Cat Damon"),
        ("duck", "Gumersindo"),
    };

    public static bool Exists(string skin) => Pets.Any(p => p.Skin == skin);

    public static (SkinLayout Layout, IReadOnlyDictionary<string, Sprite> Sprites) Load(string skin)
    {
        string Read(string folder, string file)
        {
            using var stream = typeof(SkinLoader).Assembly.GetManifestResourceStream($"skins.{folder}.{file}")
                ?? throw new InvalidOperationException($"missing skin resource {folder}/{file}");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        // The pet's own sheet is read over the shared one, extending its palette.
        return (SkinLayout.Parse(Read(skin, "skin.json")),
            SpriteSheetParser.Parse(Read("common", "sprites.txt"), Read(skin, "sprites.txt")));
    }
}
