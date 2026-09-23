using BatteryBuddy.Core.Animation;
using BatteryBuddy.Core.Scene;

namespace BatteryBuddy.Core.Tests;

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
    public void Critical_devices_blink_at_one_hertz()
    {
        var animator = New();
        animator.HasCritical = true;
        Assert.True(animator.NeedsTicks(Ms(100)));
        Assert.True(animator.FrameAt(Ms(100)).CriticalVisible);
        Assert.False(animator.FrameAt(Ms(600)).CriticalVisible);
    }
}
