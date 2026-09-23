namespace BatteryBuddy.Backends.GalaxyBuds.Samsung;

public static class SamsungBuds
{
    public static bool IsGalaxyBudsName(string name) =>
        name.Contains("Buds", StringComparison.OrdinalIgnoreCase);
}
