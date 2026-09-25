using BatteryBuddy.Devices;
using BatteryBuddy.Pet.Animation;
using BatteryBuddy.Pet.Scene;

namespace BatteryBuddy.Tests;

public class HostPickerTests
{
    static readonly PixelPoint Mouse = new(30, 34);
    const int Width = 48;
    static readonly TimeSpan T0 = TimeSpan.FromSeconds(100);

    // On channel 1; 2 paired; 3 empty.
    static readonly HostChannels Channels = new(new[] { new HostChannel(1, true), new HostChannel(2, true), new HostChannel(3, false) }, 1);

    static HostPicker Opened(HostChannels? channels = null)
    {
        var picker = new HostPicker(Width);
        picker.Open("mx-master-3s", Mouse, channels ?? Channels, T0);
        return picker;
    }

    static IReadOnlyList<PixelPoint> At(HostPicker p, TimeSpan now) => p.DrawingAt(now)!.Bubbles.Select(b => b.At).ToList();

    [Fact]
    public void Bubbles_rise_out_of_the_mouse_then_settle_on_the_arc()
    {
        var picker = Opened();
        Assert.Equal(PickerPhase.Opening, picker.Phase);
        Assert.NotEqual(ChannelArc.Positions(Mouse, 3, Width), At(picker, T0));
        Assert.False(picker.DrawingAt(T0)!.Trail);

        Assert.True(picker.Tick(T0 + HostPicker.MoveTime));
        Assert.Equal(PickerPhase.Open, picker.Phase);
        Assert.Equal(ChannelArc.Positions(Mouse, 3, Width), At(picker, T0 + HostPicker.MoveTime));
        Assert.True(picker.DrawingAt(T0 + HostPicker.MoveTime)!.Trail);
    }

    [Fact]
    public void Bubbles_settle_within_a_fifth_of_a_second()
    {
        var picker = Opened();
        picker.Tick(T0 + TimeSpan.FromMilliseconds(199));
        Assert.Equal(PickerPhase.Opening, picker.Phase);
        picker.Tick(T0 + TimeSpan.FromMilliseconds(200));
        Assert.Equal(PickerPhase.Open, picker.Phase);
    }

    [Fact]
    public void Moving_bubbles_are_redrawn_at_about_thirty_frames_a_second() =>
        Assert.InRange(HostPicker.FrameInterval, TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(40));

    [Fact]
    public void Only_a_paired_channel_other_than_the_current_one_can_be_picked()
    {
        var picker = Opened();
        Assert.Equal(new BubbleLook?[] { BubbleLook.Current, BubbleLook.Pickable, BubbleLook.Empty },
            new[] { 1, 2, 3 }.Select(picker.Look));
        Assert.False(picker.CanPick(1));
        Assert.True(picker.CanPick(2));
        Assert.False(picker.CanPick(3));
        Assert.False(picker.CanPick(4));
    }

    [Fact]
    public void Picking_the_current_channel_is_ignored()
    {
        var picker = Opened();
        picker.Pick(1, T0);
        Assert.Equal(PickerPhase.Opening, picker.Phase);
    }

    [Fact]
    public void Closes_after_eight_idle_seconds_and_hovering_counts_as_activity()
    {
        var picker = Opened();
        picker.Tick(T0 + HostPicker.MoveTime);
        picker.Hover(2, T0 + TimeSpan.FromSeconds(5));
        Assert.Equal(T0 + TimeSpan.FromSeconds(13), picker.ClosesAt);

        picker.Tick(T0 + TimeSpan.FromSeconds(12));
        Assert.Equal(PickerPhase.Open, picker.Phase);
        picker.Tick(T0 + TimeSpan.FromSeconds(13));
        Assert.Equal(PickerPhase.Closing, picker.Phase);
        picker.Tick(T0 + TimeSpan.FromSeconds(13) + HostPicker.MoveTime);
        Assert.Equal(PickerPhase.Closed, picker.Phase);
        Assert.Null(picker.DrawingAt(T0 + TimeSpan.FromSeconds(14)));
    }

    [Fact]
    public void Close_sinks_the_bubbles_back_then_closes()
    {
        var picker = Opened();
        picker.Tick(T0 + HostPicker.MoveTime);
        var closeAt = T0 + TimeSpan.FromSeconds(2);
        picker.Close(closeAt);
        Assert.Equal(PickerPhase.Closing, picker.Phase);
        Assert.True(picker.NeedsTicks);
        Assert.Equal(ChannelArc.Positions(Mouse, 3, Width), At(picker, closeAt));   // starts from the arc
        picker.Tick(closeAt + HostPicker.MoveTime);
        Assert.False(picker.IsOpen);
        Assert.Null(picker.DeviceKey);
    }

    [Fact]
    public void Picking_flies_every_bubble_into_the_chosen_one_then_closes()
    {
        var picker = Opened();
        picker.Tick(T0 + HostPicker.MoveTime);
        var pickAt = T0 + TimeSpan.FromSeconds(2);
        picker.Pick(2, pickAt);
        Assert.Equal(PickerPhase.Leaving, picker.Phase);

        var chosen = ChannelArc.Positions(Mouse, 3, Width)[1];
        Assert.All(At(picker, pickAt + HostPicker.MoveTime), p => Assert.Equal(chosen, p));
        picker.Tick(pickAt + HostPicker.MoveTime);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void Hover_marks_the_bubble()
    {
        var picker = Opened();
        picker.Hover(2, T0);
        Assert.Equal(2, picker.Hovered);
        Assert.Equal(new[] { false, true, false }, picker.DrawingAt(T0)!.Bubbles.Select(b => b.Hovered));
    }

    [Fact]
    public void Reset_closes_at_once()
    {
        var picker = Opened();
        picker.Reset();
        Assert.False(picker.IsOpen);
        Assert.Null(picker.DrawingAt(T0));
        Assert.False(picker.NeedsTicks);
    }

    [Fact]
    public void Shows_at_most_three_channels()
    {
        var five = new HostChannels(Enumerable.Range(1, 5).Select(n => new HostChannel(n, true)).ToList(), 1);
        var picker = Opened(five);
        Assert.Equal(new[] { 1, 2, 3 }, picker.DrawingAt(T0)!.Bubbles.Select(b => b.Number));
        Assert.False(picker.CanPick(4));
    }
}
