using System.IO;
using BatteryBuddy.Core.Scene;

namespace BatteryBuddy.App;

static class SkinLoader
{
    public static (SkinLayout Layout, IReadOnlyDictionary<string, Sprite> Sprites) Load(string skin)
    {
        string Read(string file)
        {
            using var stream = typeof(SkinLoader).Assembly.GetManifestResourceStream($"skins.{skin}.{file}")
                ?? throw new InvalidOperationException($"missing skin resource {skin}/{file}");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        return (SkinLayout.Parse(Read("skin.json")), SpriteSheetParser.Parse(Read("sprites.txt")));
    }
}
