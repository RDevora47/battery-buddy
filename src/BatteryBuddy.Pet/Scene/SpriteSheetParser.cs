namespace BatteryBuddy.Pet.Scene;

public static class SpriteSheetParser
{
    public static IReadOnlyDictionary<string, Sprite> Parse(string text)
    {
        var palette = new Dictionary<char, uint>();
        var sprites = new Dictionary<string, Sprite>();
        var rows = new List<string>();
        string? mode = null;
        string? name = null;
        int density = 1;
        int lineNumber = 0;

        foreach (var raw in text.Split('\n'))
        {
            lineNumber++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            if (line == "palette") { mode = "palette"; continue; }
            if (line.StartsWith("sprite "))
            {
                if (mode == "sprite") throw new FormatException($"line {lineNumber}: sprite '{name}' missing 'end'");
                var header = line["sprite ".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                name = header[0];
                density = header.Length switch
                {
                    1 => 1,
                    2 when header[1] is ['@', .. var n, 'x'] && int.TryParse(n, out var d) && d > 0 => d,
                    _ => throw new FormatException($"line {lineNumber}: expected 'sprite <name> [@<n>x]'"),
                };
                rows.Clear();
                mode = "sprite";
                continue;
            }
            if (line == "end")
            {
                if (mode != "sprite") throw new FormatException($"line {lineNumber}: 'end' outside a sprite");
                sprites[name!] = Build(name!, rows, palette, density);
                mode = null;
                continue;
            }

            switch (mode)
            {
                case "palette":
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2 || parts[0].Length != 1)
                        throw new FormatException($"line {lineNumber}: expected '<char> #rrggbb'");
                    palette[parts[0][0]] = 0xFF000000u | Convert.ToUInt32(parts[1].TrimStart('#'), 16);
                    break;
                case "sprite":
                    rows.Add(line);
                    break;
                default:
                    throw new FormatException($"line {lineNumber}: unexpected '{line}'");
            }
        }

        if (mode == "sprite") throw new FormatException($"sprite '{name}' missing 'end'");
        return sprites;
    }

    static Sprite Build(string name, List<string> rows, Dictionary<char, uint> palette, int density)
    {
        if (rows.Count == 0) throw new FormatException($"sprite '{name}' has no rows");
        int width = rows[0].Length;
        if (width % density != 0 || rows.Count % density != 0)
            throw new FormatException($"sprite '{name}' is {width}x{rows.Count}, not a multiple of its @{density}x density");
        var pixels = new uint[width * rows.Count];
        for (int y = 0; y < rows.Count; y++)
        {
            if (rows[y].Length != width)
                throw new FormatException($"sprite '{name}' row {y} has {rows[y].Length} pixels, expected {width}");
            for (int x = 0; x < width; x++)
            {
                char c = rows[y][x];
                if (c == '.') continue;
                if (!palette.TryGetValue(c, out var color))
                    throw new FormatException($"sprite '{name}' row {y}: unknown color '{c}'");
                pixels[y * width + x] = color;
            }
        }
        return new Sprite(name, width, rows.Count, pixels, density);
    }
}
