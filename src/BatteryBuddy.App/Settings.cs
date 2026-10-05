using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BatteryBuddy.Pet.Animation;
using BatteryBuddy.Pet.Scene;
using BatteryBuddy.Backend;

namespace BatteryBuddy.App;

sealed record Settings(double? Left, double? Top, BatteryStyle BatteryStyle = BatteryStyle.Outline, string Skin = SkinLoader.Default,
    FullChargeHat Hat = FullChargeHat.SaiyanHair)
{
    static readonly string FilePath = AppDataFile("settings.json");

    public static Settings Load() => JsonFile.Load(FilePath, new Settings(null, null));

    public void Save() => JsonFile.Save(FilePath, this);

    public static string AppDataFile(string name) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BatteryBuddy", name);
}

/// <summary>What the charge tracker learned about each device, kept across runs.</summary>
static class DeviceStatsFile
{
    static readonly string FilePath = Settings.AppDataFile("devices.json");

    public static Dictionary<string, DeviceChargeStats> Load() =>
        JsonFile.Load(FilePath, new Dictionary<string, DeviceChargeStats>());

    public static void Save(IReadOnlyDictionary<string, DeviceChargeStats> stats) => JsonFile.Save(FilePath, stats);
}

/// <summary>A week of how fast each device drained in use, for its time-left estimate.</summary>
static class UsageFile
{
    static readonly string FilePath = Settings.AppDataFile("usage.json");

    public static Dictionary<string, List<DrainSegment>> Load() =>
        JsonFile.Load(FilePath, new Dictionary<string, List<DrainSegment>>());

    public static void Save(IReadOnlyDictionary<string, List<DrainSegment>> segments) => JsonFile.Save(FilePath, segments);
}

static class JsonFile
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static T Load<T>(string path, T fallback)
    {
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? fallback; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return fallback;
        }
    }

    public static void Save<T>(string path, T value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write($"{Path.GetFileName(path)}: save failed: {ex.Message}");
        }
    }
}
