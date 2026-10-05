using BatteryBuddy.Pet.Ui;

namespace BatteryBuddy.Tests;

public class FullScreenPeekTests
{
    static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    readonly FullScreenPeek _peek = new();

    [Fact]
    public void Shown_while_nothing_is_full_screen()
    {
        _peek.MouseMoved(S(0));
        Assert.True(_peek.IsShown(S(100)));
        Assert.Null(_peek.HidesAt);
    }

    [Fact]
    public void Full_screen_hides_it_once_the_mouse_rests()
    {
        _peek.SetFullScreen(true, S(10));
        Assert.True(_peek.IsShown(S(12)));
        Assert.Equal(S(10) + FullScreenPeek.IdleHide, _peek.HidesAt);
        Assert.False(_peek.IsShown(S(10) + FullScreenPeek.IdleHide));
    }

    [Fact]
    public void Moving_the_mouse_brings_it_back_until_it_rests_again()
    {
        _peek.SetFullScreen(true, S(0));
        _peek.MouseMoved(S(20));
        Assert.True(_peek.IsShown(S(21)));
        Assert.Equal(S(20) + FullScreenPeek.IdleHide, _peek.HidesAt);
        Assert.False(_peek.IsShown(S(20) + FullScreenPeek.IdleHide));
    }

    [Fact]
    public void Leaving_full_screen_shows_it_for_good()
    {
        _peek.SetFullScreen(true, S(0));
        _peek.SetFullScreen(false, S(30));
        Assert.True(_peek.IsShown(S(60)));
        Assert.Null(_peek.HidesAt);
    }

    [Fact]
    public void Staying_full_screen_keeps_the_rest_timer()
    {
        _peek.SetFullScreen(true, S(0));
        _peek.SetFullScreen(true, S(2));   // re-checked (another window came forward, still full screen)
        Assert.Equal(S(0) + FullScreenPeek.IdleHide, _peek.HidesAt);
    }
}

public class FullScreenCheckTests
{
    static readonly ScreenRect Monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void A_window_covering_the_monitor_is_full_screen() =>
        Assert.True(FullScreenCheck.Covers(new ScreenRect(0, 0, 1920, 1080), Monitor));

    [Fact]
    public void A_borderless_window_spilling_past_the_edges_is_full_screen() =>
        Assert.True(FullScreenCheck.Covers(new ScreenRect(-8, -8, 1936, 1096), Monitor));

    [Fact]
    public void A_maximized_window_leaves_the_taskbar_so_is_not() =>
        Assert.False(FullScreenCheck.Covers(new ScreenRect(0, 0, 1920, 1032), Monitor));

    [Fact]
    public void A_window_on_another_monitor_is_not() =>
        Assert.False(FullScreenCheck.Covers(new ScreenRect(1920, 0, 1920, 1080), Monitor));
}
