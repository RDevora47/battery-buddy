using BatteryBuddy.Pet.Animation;
using BatteryBuddy.Pet.Scene;
using BatteryBuddy.Pet.Ui;

namespace BatteryBuddy.Tests;

public class PetAnimatorTests
{
    static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    // random() == 0 → the first idle burst comes exactly 3 s after construction.
    static PetAnimator New() => new(() => 0, TimeSpan.Zero, new PixelPoint(36, 10));

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
    public void Sleepy_shows_only_drowsy_eyes_and_worried_shows_sweat()
    {
        var animator = New();
        animator.Mood = Mood.Sleepy;
        var sleepy = animator.FrameAt(Ms(100));
        Assert.Equal("body_sleepy", sleepy.BodySprite);
        Assert.Empty(sleepy.Overlays);

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
        var animator = new PetAnimator(() => 0, TimeSpan.Zero, new PixelPoint(36, 10), magnifier: new PixelPoint(0, 22));
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
        var animator = new PetAnimator(() => 0, TimeSpan.Zero, new PixelPoint(36, 10), new PixelPoint(12, 1));
        animator.SuperSaiyan = true;
        Assert.Contains(new Overlay("saiyan", 12, 1), animator.FrameAt(Ms(100)).Overlays);
        animator.FrameAt(TimeSpan.FromSeconds(3));
        var bob = animator.FrameAt(TimeSpan.FromSeconds(3) + Ms(340));
        Assert.Contains(new Overlay("saiyan", 12, 1 + bob.BodyDy), bob.Overlays);
    }

    [Fact]
    public void Full_charge_hat_can_be_a_straw_hat_or_none()
    {
        var animator = new PetAnimator(() => 0, TimeSpan.Zero, new PixelPoint(36, 10),
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
        var animator = new PetAnimator(() => 0, TimeSpan.Zero, new PixelPoint(36, 10),
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
    public void A_reaching_limb_takes_its_frames_to_get_to_the_mouse_and_back()
    {
        // Two frames out (steps 1, 2), there (0) for the rest of the second, then two frames back (2, 1).
        var animator = new PetAnimator(() => 0, TimeSpan.Zero, new PixelPoint(36, 10)) { ReachFrames = 2 };
        var f = PetAnimator.FrameInterval.TotalMilliseconds;
        (bool, int) At(double ms) { var frame = animator.FrameAt(Ms(ms)); return (frame.RightPawOnMouse, frame.ReachStep); }

        Assert.Equal((false, 0), At(50));
        animator.Click(Ms(100));
        Assert.Equal((true, 1), At(100));
        Assert.Equal((true, 2), At(101 + f));
        Assert.Equal((true, 0), At(101 + 2 * f));
        Assert.Equal((true, 0), At(1099));
        Assert.Equal((true, 2), At(1100));          // letting go
        Assert.Equal((true, 1), At(1101 + f));
        Assert.True(animator.NeedsTicks(Ms(1101 + f)));
        Assert.Equal((false, 0), At(1101 + 2 * f));
        Assert.False(animator.NeedsTicks(Ms(1101 + 2 * f)));

        animator.Click(Ms(2000));
        animator.Click(Ms(2001 + 2 * f));           // already there: no second trip
        Assert.Equal((true, 0), At(2001 + 2 * f));
        animator.KeyTap(Ms(2500));                  // typing sends it back at once
        Assert.Equal((true, 2), At(2500));
        Assert.Equal((false, 0), At(2501 + 2 * f));
    }

    [Fact]
    public void Typing_without_the_mouse_never_sends_a_reaching_limb_anywhere()
    {
        var animator = new PetAnimator(() => 0, TimeSpan.Zero, new PixelPoint(36, 10)) { ReachFrames = 2 };
        animator.KeyTap(Ms(100));
        var frame = animator.FrameAt(Ms(100));
        Assert.Equal((false, 0), (frame.RightPawOnMouse, frame.ReachStep));
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
        var animator = new PetAnimator(() => 0, TimeSpan.Zero, new PixelPoint(36, 10), new PixelPoint(12, 1))
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
        var animator = new PetAnimator(() => 0, TimeSpan.Zero, new PixelPoint(36, 10)) { Hops = true };
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

    static readonly PixelPoint TopHat = new(9, 2);

    static PetAnimator WithTopHat() => new(() => 0, TimeSpan.Zero, new PixelPoint(36, 10), tophat: TopHat);

    static TimeSpan Frames(int n) => PetAnimator.FrameInterval * n;

    [Fact]
    public void The_trick_puts_on_the_top_hat_then_presses_a_key()
    {
        var animator = WithTopHat();
        animator.BeginTrick(Ms(1000));
        Assert.True(animator.InTrick);
        var hatOn = animator.FrameAt(Ms(1000));
        Assert.Contains(new Overlay("tophat", TopHat.X, TopHat.Y), hatOn.Overlays);
        Assert.Equal(0, hatOn.PawLeftDy + hatOn.PawRightDy);   // not yet
        Assert.Null(hatOn.Smoke);

        Assert.True(animator.NeedsTicks(Ms(1100)));
        var press = animator.FrameAt(Ms(1000) + Frames(1));
        Assert.Equal(1, press.PawLeftDy + press.PawRightDy);   // one paw, one frame
        Assert.Null(press.Smoke);   // the key sets the bomb off
        var after = animator.FrameAt(Ms(1000) + Frames(2));
        Assert.Equal(0, after.PawLeftDy + after.PawRightDy);
    }

    [Fact]
    public void The_smoke_bomb_goes_off_after_the_key_and_covers_the_pet_at_the_hide()
    {
        var animator = WithTopHat();
        var start = Ms(1000);
        animator.BeginTrick(start);
        Assert.Null(animator.FrameAt(start + Frames(2) - Ms(1)).Smoke);
        for (int n = 1; n <= PetAnimator.SmokeFrames; n++)
            Assert.Equal($"smokebomb{n}", animator.FrameAt(start + Frames(1 + n)).Smoke);
        // Frame 3, the one covering the whole canvas, lands exactly when the pet starts fading.
        Assert.Equal("smokebomb3", animator.FrameAt(start + FullScreenPeek.TrickLead).Smoke);
        Assert.Equal($"smokebomb{PetAnimator.SmokeFrames}", animator.FrameAt(start + TimeSpan.FromSeconds(10)).Smoke);   // last frame holds
        Assert.True(animator.NeedsTicks(start + Frames(1 + PetAnimator.SmokeFrames) - Ms(1)));
        Assert.False(animator.NeedsTicks(start + Frames(1 + PetAnimator.SmokeFrames)));   // invisible by then: no more frames
    }

    [Fact]
    public void The_pet_is_gone_once_the_smoke_covers_it()
    {
        // From the covering frame on, the smoke thins into wisps while the window fades: nothing is left behind it.
        var animator = WithTopHat();
        animator.BeginTrick(Ms(0));
        Assert.False(animator.FrameAt(Frames(3)).Vanished);   // smoke frame 2: still billowing up
        Assert.True(animator.FrameAt(FullScreenPeek.TrickLead).Vanished);
        Assert.True(animator.FrameAt(Ms(5000)).Vanished);
        animator.CancelTrick();
        Assert.False(animator.FrameAt(Ms(5100)).Vanished);   // back, as the mouse moved
    }

    [Fact]
    public void Hiding_without_the_lead_still_shows_the_covering_smoke()
    {
        var animator = WithTopHat();
        animator.Vanish(Ms(5000));
        Assert.True(animator.InTrick);
        Assert.Equal("smokebomb3", animator.FrameAt(Ms(5000)).Smoke);
    }

    [Fact]
    public void Hiding_again_while_gone_does_not_restart_the_smoke()
    {
        var animator = WithTopHat();
        animator.BeginTrick(Ms(0));
        animator.Vanish(FullScreenPeek.TrickLead);
        animator.Vanish(Ms(5000));   // full screen re-checked while hidden
        Assert.Equal($"smokebomb{PetAnimator.SmokeFrames}", animator.FrameAt(Ms(5000)).Smoke);
    }

    [Fact]
    public void Cancelling_takes_off_the_hat_and_clears_the_smoke()
    {
        var animator = WithTopHat();
        animator.BeginTrick(Ms(0));
        animator.FrameAt(Frames(3));   // smoke going
        animator.CancelTrick();
        var frame = animator.FrameAt(Frames(4));
        Assert.False(animator.InTrick);
        Assert.Null(frame.Smoke);
        Assert.DoesNotContain(frame.Overlays, o => o.Sprite == "tophat");
    }

    [Fact]
    public void The_top_hat_replaces_a_full_charge_hat_during_the_trick()
    {
        var animator = WithTopHat();
        animator.SuperSaiyan = true;
        animator.BeginTrick(Ms(0));
        var frame = animator.FrameAt(Ms(0));
        Assert.Contains(frame.Overlays, o => o.Sprite == "tophat");
        Assert.DoesNotContain(frame.Overlays, o => o.Sprite is "saiyan" or "strawhat");
    }

    [Fact]
    public void No_idle_burst_during_the_trick()
    {
        var animator = WithTopHat();   // first idle burst due at 3 s
        animator.BeginTrick(TimeSpan.FromSeconds(2.9));
        var frame = animator.FrameAt(TimeSpan.FromSeconds(3) + Ms(340));   // the burst's blink frame
        Assert.Equal("body_idle", frame.BodySprite);
    }

    static readonly PixelPoint Headband = new(11, 9);

    static PetAnimator Ninja() => new(() => 0, TimeSpan.Zero, new PixelPoint(36, 10), tophat: TopHat, headband: Headband)
    {
        VanishStyle = VanishStyle.Ninja,
    };

    [Fact]
    public void The_ninja_ties_on_a_fluttering_headband_instead_of_the_top_hat()
    {
        var animator = Ninja();
        animator.BeginTrick(Ms(0));
        var first = animator.FrameAt(Frames(0));
        Assert.Contains(new Overlay("headband1", Headband.X, Headband.Y), first.Overlays);
        Assert.Contains(new Overlay("headband2", Headband.X, Headband.Y), animator.FrameAt(Frames(1)).Overlays);   // tails flutter
        Assert.Contains(new Overlay("headband1", Headband.X, Headband.Y), animator.FrameAt(Frames(2)).Overlays);
        Assert.DoesNotContain(first.Overlays, o => o.Sprite == "tophat");
    }

    [Fact]
    public void The_ninja_vanishes_in_smoke_and_leaves()
    {
        var animator = Ninja();
        animator.BeginTrick(Ms(0));
        Assert.Null(animator.FrameAt(Frames(1)).Leaves);
        for (int n = 1; n <= PetAnimator.SmokeFrames; n++)
        {
            var frame = animator.FrameAt(Frames(1 + n));
            Assert.Equal($"smokebomb{n}", frame.Smoke);
            Assert.Equal($"leaves{n}", frame.Leaves);
        }
    }

    [Fact]
    public void The_magician_smoke_has_no_leaves()
    {
        var animator = WithTopHat();
        animator.BeginTrick(Ms(0));
        Assert.All(Enumerable.Range(0, 8), n => Assert.Null(animator.FrameAt(Frames(n)).Leaves));
    }

    [Fact]
    public void Just_fading_performs_no_trick()
    {
        var animator = WithTopHat();
        animator.VanishStyle = VanishStyle.Fade;
        animator.BeginTrick(Ms(0));
        animator.Vanish(FullScreenPeek.TrickLead);
        Assert.False(animator.InTrick);
        var frame = animator.FrameAt(FullScreenPeek.TrickLead);
        Assert.Null(frame.Smoke);
        Assert.False(frame.Vanished);   // the window's fade alone hides it
        Assert.DoesNotContain(frame.Overlays, o => o.Sprite is "tophat" or "headband1" or "headband2");
    }
}
