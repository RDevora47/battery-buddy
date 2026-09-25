namespace BatteryBuddy.Pet.Ui;

/// <summary>What the pet says about switching the mouse's channel. An untested model's failures ask for the dev.</summary>
public static class SwitchText
{
    public const string CantSwitch = "Can't switch this mouse yet. Tell the dev!";
    const string NotAnswering = "Mouse isn't answering. Turn it off and on.";
    const string Failed = "Couldn't switch. Click the mouse and retry.";

    public static string Switched(int channel) => $"→ channel {channel}";

    public static string Empty(int channel) => $"Channel {channel} is empty. Pair it first.";

    /// <summary>The channels couldn't be read.</summary>
    public static string NoAnswer(bool verified) => verified ? NotAnswering : CantSwitch;

    public static string SwitchFailed(bool verified) => verified ? Failed : CantSwitch;
}
