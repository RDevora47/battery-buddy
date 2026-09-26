using BatteryBuddy.Pet.Ui;
using Xunit;

namespace BatteryBuddy.Tests;

public class ClickStreakTests
{
    static TimeSpan Ms(int ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void Third_quick_click_completes_the_streak()
    {
        var streak = new ClickStreak(3, Ms(500));
        Assert.False(streak.Register(Ms(0)));
        Assert.False(streak.Register(Ms(300)));
        Assert.True(streak.Register(Ms(600)));
    }

    [Fact]
    public void A_slow_click_starts_over()
    {
        var streak = new ClickStreak(3, Ms(500));
        streak.Register(Ms(0));
        streak.Register(Ms(300));
        Assert.False(streak.Register(Ms(1000)));
        Assert.False(streak.Register(Ms(1200)));
        Assert.True(streak.Register(Ms(1400)));
    }

    [Fact]
    public void A_fourth_click_does_not_fire_again()
    {
        var streak = new ClickStreak(3, Ms(500));
        streak.Register(Ms(0));
        streak.Register(Ms(100));
        streak.Register(Ms(200));
        Assert.False(streak.Register(Ms(300)));
    }

    [Fact]
    public void Reset_forgets_earlier_clicks()
    {
        var streak = new ClickStreak(3, Ms(500));
        streak.Register(Ms(0));
        streak.Register(Ms(100));
        streak.Reset();
        Assert.False(streak.Register(Ms(200)));
    }
}
