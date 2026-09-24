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
    public void Happy_burst_wags_the_tail_then_rests_it()
    {
        var animator = New();
        Assert.Equal("tail", animator.FrameAt(Ms(100)).Tail);

        // One tail pose per frame of the burst, swinging side to side, back to rest at the end.
        animator.FrameAt(TimeSpan.FromSeconds(3));   // the burst starts
        var tails = Enumerable.Range(0, 6).Select(f => animator.FrameAt(TimeSpan.FromSeconds(3) + Ms((f + 0.5) * 1000.0 / PetAnimator.Fps)).Tail).ToList();
        Assert.Equal(new[] { "tail_wag1", "tail_wag2", "tail_wag1", "tail_wag2", "tail_wag1", "tail" }, tails);
        Assert.Equal("tail", animator.FrameAt(TimeSpan.FromSeconds(3) + Ms(1100)).Tail);
    }

    [Theory]
    [InlineData(Mood.Sleepy)]
    [InlineData(Mood.Worried)]
    public void Only_a_happy_pet_wags(Mood mood)
    {
        var animator = New();
        animator.Mood = mood;
        animator.FrameAt(TimeSpan.FromSeconds(3));
        Assert.Equal("tail", animator.FrameAt(TimeSpan.FromSeconds(3) + Ms(10)).Tail);
        Assert.Equal("tail", animator.FrameAt(TimeSpan.FromSeconds(3) + Ms(200)).Tail);
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
    public void Scanning_sweeps_a_magnifying_glass_that_the_eyes_follow()
    {
        var animator = new PetAnimator(() => 0, TimeSpan.Zero, new PixelPoint(37, 5), new PixelPoint(36, 10), magnifier: new PixelPoint(0, 22));
        animator.BeginSniff(TimeSpan.Zero);
        var frames = Enumerable.Range(0, 8).Select(f => animator.FrameAt(Ms((f + 0.5) * 1000.0 / PetAnimator.Fps))).ToList();
        var glass = frames.Select(f => Assert.Single(f.Overlays, o => o.Sprite == "magnifier")).ToList();
        Assert.Equal(new[] { 0, 1, 2, 3, 3, 2, 1, 0 }, glass.Select(o => o.X));   // out and back
        Assert.Equal(new[] { 22, 21, 21, 22, 22, 21, 21, 22 }, glass.Select(o => o.Y));   // along an arc
        Assert.Equal(glass.Select(o => o.X < 2 ? "body_sniff_l" : "body_sniff_r"), frames.Select(f => f.BodySprite));

        animator.EndSniff(Ms(10));
        Assert.DoesNotContain(animator.FrameAt(Ms(1400)).Overlays, o => o.Sprite == "magnifier");
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
    public void Super_saiyan_gives_the_pet_saiyan_hair_that_bobs_with_it()
    {
        var animator = new PetAnimator(() => 0, TimeSpan.Zero, new PixelPoint(37, 5), new PixelPoint(36, 10), new PixelPoint(12, 1));
        animator.SuperSaiyan = true;
        Assert.Contains(new Overlay("saiyan", 12, 1), animator.FrameAt(Ms(100)).Overlays);
        animator.FrameAt(TimeSpan.FromSeconds(3));
        var bob = animator.FrameAt(TimeSpan.FromSeconds(3) + Ms(340));
        Assert.Contains(new Overlay("saiyan", 12, 1 + bob.BodyDy), bob.Overlays);
    }

    [Fact]
    public void Full_charge_hat_can_be_a_straw_hat_or_none()
    {
        var animator = new PetAnimator(() => 0, TimeSpan.Zero, new PixelPoint(37, 5), new PixelPoint(36, 10),
            new PixelPoint(12, 1), strawHat: new PixelPoint(10, 6));
        animator.SuperSaiyan = true;
        Assert.Equal(FullChargeHat.SaiyanHair, animator.Hat);

        animator.Hat = FullChargeHat.StrawHat;
        Assert.Contains(new Overlay("strawhat", 10, 6), animator.FrameAt(Ms(100)).Overlays);
        Assert.DoesNotContain(animator.FrameAt(Ms(100)).Overlays, o => o.Sprite == "saiyan");
        animator.FrameAt(TimeSpan.FromSeconds(3));
        var bob = animator.FrameAt(TimeSpan.FromSeconds(3) + Ms(340));
        Assert.Contains(new Overlay("strawhat", 10, 6 + bob.BodyDy), bob.Overlays);   // bobs with the pet

        animator.Hat = FullChargeHat.None;
        Assert.Empty(animator.FrameAt(TimeSpan.FromSeconds(5)).Overlays);
    }

    [Fact]
    public void No_hat_until_every_device_is_nearly_full()
    {
        var animator = new PetAnimator(() => 0, TimeSpan.Zero, new PixelPoint(37, 5), new PixelPoint(36, 10),
            new PixelPoint(12, 1), strawHat: new PixelPoint(10, 6)) { Hat = FullChargeHat.StrawHat };
        Assert.Empty(animator.FrameAt(Ms(100)).Overlays);
    }

    [Fact]
    public void Critical_devices_are_shaken_instead_of_the_idle_bounce()
    {
        var animator = New();
        animator.HasCritical = true;
        Assert.Equal(0, animator.FrameAt(Ms(100)).CriticalDx);   // no shaking between idle bursts
        Assert.False(animator.NeedsTicks(Ms(100)));

        var start = animator.FrameAt(Ms(3000));
        var mid = animator.FrameAt(Ms(3200));
        Assert.Equal((-1, 1), (start.CriticalDx, mid.CriticalDx));
        Assert.Equal((0, "body_idle"), (animator.FrameAt(Ms(3340)).BodyDy, animator.FrameAt(Ms(3340)).BodySprite)); // no bob or blink
        Assert.Equal("tail", mid.Tail);   // nor a wag
        Assert.Equal(0, animator.FrameAt(Ms(4100)).CriticalDx);
    }

    [Fact]
    public void Critical_shake_waits_while_the_paws_are_busy()
    {
        var animator = New();
        animator.HasCritical = true;
        animator.Click(Ms(2900));
        animator.FrameAt(Ms(3000));
        Assert.Equal(0, animator.FrameAt(Ms(3200)).CriticalDx);
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
    public void Scrolling_flicks_the_right_paw_on_the_mouse()
    {
        var animator = New();
        animator.Scroll(Ms(500));
        var frame = animator.FrameAt(Ms(500));
        Assert.Equal((0, 1, 1, true), (frame.PawLeftDy, frame.PawRightDy, frame.ClickDy, frame.RightPawOnMouse));
        Assert.True(animator.FrameAt(Ms(1400)).RightPawOnMouse);
        Assert.False(animator.FrameAt(Ms(1600)).RightPawOnMouse);
    }

    [Fact]
    public void Continuous_scrolling_wiggles_instead_of_holding_the_paw_down()
    {
        var animator = New();
        animator.Scroll(Ms(500));
        animator.Scroll(Ms(550));   // still pressed from the first notch
        animator.Scroll(Ms(700));   // one frame of rest before the next flick
        Assert.Equal(0, animator.FrameAt(Ms(700)).PawRightDy);
        animator.Scroll(Ms(850));
        Assert.Equal(1, animator.FrameAt(Ms(850)).PawRightDy);
    }

    [Fact]
    public void Idle_bounce_waits_while_the_paws_are_busy()
    {
        var animator = New();
        animator.KeyTap(Ms(2950));
        animator.FrameAt(Ms(3000));                        // bounce was due, but a paw is down
        Assert.Equal(0, animator.FrameAt(Ms(3200)).BodyDy); // would be mid-bob otherwise
        Assert.True(animator.NextIdleAt >= Ms(6000));
    }

    [Fact]
    public void Idle_bounce_stops_when_another_animation_starts()
    {
        var animator = New();
        animator.FrameAt(Ms(3000));
        Assert.Equal(1, animator.FrameAt(Ms(3200)).BodyDy);
        animator.Click(Ms(3250));
        Assert.Equal(0, animator.FrameAt(Ms(3400)).BodyDy);
        Assert.Equal(0, animator.FrameAt(Ms(3700)).BodyDy); // not resumed once the click is over
    }

    [Fact]
    public void Idle_bounce_waits_for_connect_effects_and_sniffing()
    {
        var whoosh = New();
        whoosh.AddWhoosh(10, 10, Ms(2900));
        whoosh.FrameAt(Ms(3000));
        Assert.Equal(0, whoosh.FrameAt(Ms(3200)).BodyDy);

        var sniff = New();
        sniff.BeginSniff(Ms(2900));
        sniff.EndSniff(Ms(2950));
        sniff.FrameAt(Ms(3000));
        Assert.Equal(0, sniff.FrameAt(Ms(4000)).BodyDy);
    }

    [Fact]
    public void Typing_brings_the_paw_back_from_the_mouse()
    {
        var animator = New();
        animator.Click(Ms(100));
        animator.KeyTap(Ms(400));
        Assert.False(animator.FrameAt(Ms(400)).RightPawOnMouse);
    }

    [Fact]
    public void A_perched_pet_hops_from_key_to_key_as_it_types()
    {
        var animator = new PetAnimator(() => 0, TimeSpan.Zero, new PixelPoint(37, 5), new PixelPoint(36, 10), new PixelPoint(12, 1))
        {
            Hops = true,
            SuperSaiyan = true,
        };
        animator.KeyTap(Ms(100));
        var left = animator.FrameAt(Ms(100));
        Assert.Equal((-1, -PetAnimator.HopHeight), (left.BodyDx, left.BodyDy));
        Assert.Contains(new Overlay("saiyan", 11, 1 - PetAnimator.HopHeight), left.Overlays);   // the hair hops along

        animator.KeyTap(Ms(150));
        var right = animator.FrameAt(Ms(150));
        Assert.Equal((1, -PetAnimator.HopHeight), (right.BodyDx, right.BodyDy));

        var landed = animator.FrameAt(Ms(150) + PetAnimator.FrameInterval);
        Assert.Equal((0, 0), (landed.BodyDx, landed.BodyDy));
    }

    [Fact]
    public void A_perched_pet_stays_put_when_clicking()
    {
        var animator = new PetAnimator(() => 0, TimeSpan.Zero, new PixelPoint(37, 5), new PixelPoint(36, 10)) { Hops = true };
        animator.Click(Ms(100));
        var frame = animator.FrameAt(Ms(100));
        Assert.Equal((0, 0, 1), (frame.BodyDx, frame.BodyDy, frame.ClickDy));   // only the mouse dips
    }

    [Fact]
    public void Other_pets_type_without_hopping()
    {
        var animator = New();
        animator.KeyTap(Ms(100));
        var frame = animator.FrameAt(Ms(100));
        Assert.Equal((0, 0, 1), (frame.BodyDx, frame.BodyDy, frame.PawLeftDy));
    }
}
