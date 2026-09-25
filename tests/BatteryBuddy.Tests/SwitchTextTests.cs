using BatteryBuddy.Pet.Ui;

namespace BatteryBuddy.Tests;

public class SwitchTextTests
{
    [Fact]
    public void Messages_match_the_spec()
    {
        Assert.Equal("→ channel 2", SwitchText.Switched(2));
        Assert.Equal("Channel 3 is empty. Pair it first.", SwitchText.Empty(3));
        Assert.Equal("Can't switch this mouse yet. Tell the dev!", SwitchText.CantSwitch);
    }

    [Fact]
    public void A_verified_mouse_gets_advice_and_an_untested_one_asks_for_the_dev()
    {
        Assert.Equal("Mouse isn't answering. Turn it off and on.", SwitchText.NoAnswer(verified: true));
        Assert.Equal(SwitchText.CantSwitch, SwitchText.NoAnswer(verified: false));
        Assert.Equal("Couldn't switch. Click the mouse and retry.", SwitchText.SwitchFailed(verified: true));
        Assert.Equal(SwitchText.CantSwitch, SwitchText.SwitchFailed(verified: false));
    }
}
