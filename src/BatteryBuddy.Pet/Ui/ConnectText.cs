using BatteryBuddy.Devices;

namespace BatteryBuddy.Pet.Ui;

/// <summary>What the pet says when calling a remembered device.</summary>
public static class ConnectText
{
    public static string Calling(string name) => $"Calling {name}…";

    public static string For(ConnectResult result, string name) => result switch
    {
        ConnectResult.Requested => $"Asked {name} to connect.",
        ConnectResult.NotFound => $"Can't find {name}. Is it paired?",
        _ => $"{name} didn't come. Is it out of its case?",
    };
}
