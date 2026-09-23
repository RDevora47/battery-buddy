using BatteryBuddy.Pet.Animation;
using BatteryBuddy.Pet.Scene;

namespace BatteryBuddy.Tests;

public class PetAnimatorTests
{
    static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    // random() == 0 → the first idle burst comes exactly 3 s after construction.
    static PetAnimator New() => new(() => 0, TimeSpan.Zero, new PixelPoint(37, 5), new PixelPoint(36, 10));

    [Fact]
    public void Happy_rests_on_idle_frame_without_ticks()
    {
        var animator = New();
        var frame = animator.FrameAt(Ms(100));
        Assert.Equal("body_idle", frame.BodySprite);
        Assert.Equal(0, frame.BodyDy);
        Assert.Empty(frame.Overlays);
        Assert.False(animator.NeedsTicks(Ms(100)));
        Assert.Equal(TimeSpan.FromSeconds(3), animator.NextIdleAt);
    }

    [Fact]
    public void Sleepy_shows_zzz_and_worried_shows_sweat()
    {
        var animator = New();
        animator.Mood = Mood.Sleepy;
        var sleepy = animator.FrameAt(Ms(100));
        Assert.Equal("body_sleepy", sleepy.BodySprite);
        Assert.Equal(new Overlay("zzz", 37, 5), Assert.Single(sleepy.Overlays));

        animator.Mood = Mood.Worried;
        var worried = animator.FrameAt(Ms(200));
        Assert.Equal("body_worried", worried.BodySprite);
        Assert.Equal("sweat", Assert.Single(worried.Overlays).Sprite);
    }

    [Fact]
    public void Idle_burst_bobs_and_blinks_then_rests()
    {
        var animator = New();
        animator.FrameAt(TimeSpan.FromSeconds(3));
        Assert.True(animator.NeedsTicks(TimeSpan.FromSeconds(3)));

        var blink = animator.FrameAt(TimeSpan.FromSeconds(3) + Ms(340));
        Assert.Equal("body_blink", blink.BodySprite);
        Assert.Equal(1, blink.BodyDy);

        Assert.False(animator.NeedsTicks(TimeSpan.FromSeconds(3) + Ms(1100)));
        Assert.Equal(TimeSpan.FromSeconds(6), animator.NextIdleAt);
    }

    [Fact]
    public void Sniff_alternates_and_lasts_at_least_one_second()
    {
        var animator = New();
        animator.BeginSniff(TimeSpan.Zero);
        Assert.Equal("body_sniff_l", animator.FrameAt(Ms(10)).BodySprite);
        Assert.Equal("body_sniff_r", animator.FrameAt(Ms(340)).BodySprite);

        animator.EndSniff(Ms(200));
        Assert.True(animator.NeedsTicks(Ms(500)));
        Assert.StartsWith("body_sniff", animator.FrameAt(Ms(500)).BodySprite);
        Assert.Equal("body_idle", animator.FrameAt(Ms(1010)).BodySprite);
    }

    [Fact]
    public void Ploof_plays_three_smoke_frames_with_text()
    {
        var animator = New();
        animator.AddPloof(20, 20, TimeSpan.Zero);
        var first = animator.FrameAt(Ms(10));
        Assert.Contains(new Overlay("smoke1", 18, 18), first.Overlays);
        Assert.Contains(new Overlay("ploof_text", 12, 12), first.Overlays);
        Assert.Contains(animator.FrameAt(Ms(400)).Overlays, o => o.Sprite == "smoke3");
        Assert.True(animator.NeedsTicks(Ms(400)));
        Assert.Empty(animator.FrameAt(Ms(600)).Overlays);
    }

    [Fact]
    public void Whoosh_plays_three_frames_then_ends()
    {
        var animator = New();
        animator.AddWhoosh(20, 20, TimeSpan.Zero);
        Assert.Contains(new Overlay("whoosh1", 17, 19), animator.FrameAt(Ms(10)).Overlays);
        Assert.Contains(animator.FrameAt(Ms(400)).Overlays, o => o.Sprite == "whoosh3");
        Assert.True(animator.NeedsTicks(Ms(400)));
        Assert.Empty(animator.FrameAt(Ms(600)).Overlays);
        Assert.False(animator.NeedsTicks(Ms(600)));
    }

    [Fact]
    public void All_full_gives_the_pet_saiyan_hair_that_bobs_with_it()
    {
        var animator = new PetAnimator(() => 0, TimeSpan.Zero, new PixelPoint(37, 5), new PixelPoint(36, 10), new PixelPoint(12, 1));
        animator.AllFull = true;
        Assert.Contains(new Overlay("saiyan", 12, 1), animator.FrameAt(Ms(100)).Overlays);
        animator.FrameAt(TimeSpan.FromSeconds(3));
        var bob = animator.FrameAt(TimeSpan.FromSeconds(3) + Ms(340));
        Assert.Contains(new Overlay("saiyan", 12, 1 + bob.BodyDy), bob.Overlays);
    }

    [Fact]
    public void Critical_devices_shake_for_a_second_then_rest()
    {
        var animator = New();
        animator.HasCritical = true;
        Assert.True(animator.NeedsTicks(Ms(100)));
        Assert.Equal(-1, animator.FrameAt(Ms(100)).CriticalDx);
        Assert.Equal(1, animator.FrameAt(Ms(250)).CriticalDx);
        Assert.Equal(0, animator.FrameAt(Ms(1500)).CriticalDx);
        Assert.Equal(-1, animator.FrameAt(Ms(2100)).CriticalDx);
    }

    [Theory]
    [InlineData(null, "gills_perky", 0.0)]
    [InlineData(80, "gills_perky", 0.0)]
    [InlineData(50, "gills_droopy", 0.0)]
    [InlineData(25, "gills_droopy", 0.35)]
    [InlineData(20, "gills_limp", 0.42)]
    [InlineData(0, "gills_limp", 0.7)]
    public void Gills_droop_and_color_fades_with_the_lowest_battery(int? lowest, string gills, double fade)
    {
        var animator = New();
        animator.LowestBattery = lowest;
        var frame = animator.FrameAt(Ms(100));
        Assert.Equal(gills, frame.Gills);
        Assert.Equal(fade, frame.Fade, 3);
    }

    [Fact]
    public void Typing_taps_the_paws_in_turn()
    {
        var animator = New();
        animator.KeyTap(Ms(100));
        var first = animator.FrameAt(Ms(100));
        Assert.Equal((1, 0, 0), (first.PawLeftDy, first.PawRightDy, first.ClickDy));
        Assert.True(animator.NeedsTicks(Ms(100)));

        animator.KeyTap(Ms(150));   // the next key lifts the left paw and presses the right
        var second = animator.FrameAt(Ms(150));
        Assert.Equal((0, 1, 0), (second.PawLeftDy, second.PawRightDy, second.ClickDy));   // the mouse stays put
    }

    [Fact]
    public void A_press_lasts_one_frame()
    {
        var animator = New();
        animator.KeyTap(Ms(100));
        var released = Ms(100) + PetAnimator.FrameInterval;
        Assert.Equal(0, animator.FrameAt(released).PawLeftDy);
        Assert.False(animator.NeedsTicks(released));
    }

    [Fact]
    public void Clicking_presses_only_the_right_paw()
    {
        var animator = New();
        animator.Click(Ms(100));
        animator.Click(Ms(120));
        var frame = animator.FrameAt(Ms(120));
        Assert.Equal((0, 1, 1), (frame.PawLeftDy, frame.PawRightDy, frame.ClickDy));
    }

    [Fact]
    public void Clicking_puts_the_right_paw_on_the_mouse_until_a_second_passes()
    {
        var animator = New();
        Assert.False(animator.FrameAt(Ms(50)).RightPawOnMouse);
        animator.Click(Ms(100));
        Assert.True(animator.FrameAt(Ms(100)).RightPawOnMouse);

        var resting = animator.FrameAt(Ms(600));   // press over, paw still on the mouse
        Assert.Equal((true, 0), (resting.RightPawOnMouse, resting.PawRightDy));
        Assert.True(animator.NeedsTicks(Ms(600)));

        Assert.False(animator.FrameAt(Ms(1100)).RightPawOnMouse);
        Assert.False(animator.NeedsTicks(Ms(1100)));
    }

    [Fact]
    public void Typing_brings_the_paw_back_from_the_mouse()
    {
        var animator = New();
        animator.Click(Ms(100));
        animator.KeyTap(Ms(400));
        Assert.False(animator.FrameAt(Ms(400)).RightPawOnMouse);
    }
}
