using BatteryBuddy.Backends.Logitech.Hidpp;
using BatteryBuddy.Devices;

namespace BatteryBuddy.Tests;

public class HidppHostsTests
{
    const byte Direct = 0xFF, ChangeHost = 0x0A, HostsInfo = 0x0B;
    static readonly Func<TimeSpan, CancellationToken, Task> NoDelay = (_, _) => Task.CompletedTask;
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    static byte[] Reply(byte feature, byte function, params byte[] p)
    {
        var r = new byte[HidppProtocol.LongLength];
        r[0] = HidppProtocol.LongReport;
        r[1] = Direct;
        r[2] = feature;
        r[3] = (byte)(function << 4 | HidppProtocol.SoftwareId);
        p.CopyTo(r, 4);
        return r;
    }

    /// <summary>Answers from a script keyed by (feature index, function, first param) and records every frame sent.</summary>
    sealed class FakeDevice
    {
        public readonly List<byte[]> Sent = new();
        public Func<byte, byte, byte, byte[]?> Answer = (_, _, _) => null;

        public Task<byte[]?> Request(byte[] frame, TimeSpan timeout, CancellationToken ct)
        {
            Sent.Add(frame);
            return Task.FromResult(Answer(frame[2], (byte)(frame[3] >> 4), frame[4]));
        }
    }

    [Fact]
    public void Frames_carry_the_zero_based_host()
    {
        Assert.Equal(new byte[] { 0x11, 0xFF, 0x0A, 0x0B }, HidppHosts.GetHostInfo(Direct, ChangeHost)[..4]);
        Assert.Equal(new byte[] { 0x11, 0xFF, 0x0A, 0x1B, 0x01 }, HidppHosts.SetCurrentHost(Direct, ChangeHost, 2)[..5]);
        Assert.Equal(new byte[] { 0x11, 0xFF, 0x0B, 0x1B, 0x02 }, HidppHosts.GetHostStatus(Direct, HostsInfo, 3)[..5]);
    }

    [Fact]
    public void Host_info_gives_the_channel_count_and_the_current_one()
    {
        // The MX Master 3S probe of 2026-09-25: 3 channels, on the first.
        var hosts = HidppHosts.ParseHostInfo(new byte[] { 0x03, 0x00 })!;
        Assert.Equal(new[] { 1, 2, 3 }, hosts.Channels.Select(c => c.Number));
        Assert.All(hosts.Channels, c => Assert.True(c.Paired));
        Assert.Equal(1, hosts.Current);
        Assert.Equal(3, HidppHosts.ParseHostInfo(new byte[] { 0x03, 0x02 })!.Current);
    }

    [Theory]
    [InlineData(new byte[] { 0x00, 0x00 })]   // no channels
    [InlineData(new byte[] { 0x03, 0x03 })]   // current past the end
    [InlineData(new byte[] { 0x03 })]         // short
    public void Host_info_parsing_rejects_malformed_replies(byte[] p) => Assert.Null(HidppHosts.ParseHostInfo(p));

    [Fact]
    public void Host_status_says_whether_a_channel_is_paired()
    {
        Assert.True(HidppHosts.ParsePaired(new byte[] { 0x01, 0x01, 0x04, 0x01, 0x0B, 0x18 }, 2));   // probe reply, channel 2
        Assert.False(HidppHosts.ParsePaired(new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x18 }, 3));
        Assert.Null(HidppHosts.ParsePaired(new byte[] { 0x00, 0x01 }, 2));   // about another host
        Assert.Null(HidppHosts.ParsePaired(new byte[] { 0x01 }, 2));         // short
    }

    [Fact]
    public async Task Reading_marks_empty_channels_and_skips_the_current_one()
    {
        var device = new FakeDevice
        {
            Answer = (feature, function, host) => (feature, function) switch
            {
                (ChangeHost, 0) => Reply(ChangeHost, 0, 0x03, 0x00),
                (HostsInfo, 1) => Reply(HostsInfo, 1, host, (byte)(host == 2 ? 0x00 : 0x01)),
                _ => null,
            },
        };
        var hosts = (await HidppHosts.ReadAsync(device.Request, Direct, ChangeHost, HostsInfo, Timeout, CancellationToken.None))!;

        Assert.Equal(new[] { true, true, false }, hosts.Channels.Select(c => c.Paired));
        Assert.Equal(1, hosts.Current);
        Assert.DoesNotContain(device.Sent, f => f[2] == HostsInfo && f[4] == 0);   // no status request for the current channel
    }

    [Fact]
    public async Task A_status_that_cannot_be_read_leaves_the_channel_pickable()
    {
        var device = new FakeDevice
        {
            Answer = (feature, function, host) => (feature, function, host) switch
            {
                (ChangeHost, 0, _) => Reply(ChangeHost, 0, 0x03, 0x00),
                (HostsInfo, 1, 1) => Reply(HostsInfo, 1, 0x00, 0x00),   // wrong host in the reply
                _ => null,                                               // channel 3: silent
            },
        };
        var hosts = (await HidppHosts.ReadAsync(device.Request, Direct, ChangeHost, HostsInfo, Timeout, CancellationToken.None))!;
        Assert.All(hosts.Channels, c => Assert.True(c.Paired));
    }

    [Fact]
    public async Task Without_hosts_info_every_channel_counts_as_paired()
    {
        var device = new FakeDevice { Answer = (f, fn, _) => f == ChangeHost && fn == 0 ? Reply(ChangeHost, 0, 0x02, 0x01) : null };
        var hosts = (await HidppHosts.ReadAsync(device.Request, Direct, ChangeHost, 0, Timeout, CancellationToken.None))!;

        Assert.Equal(new[] { true, true }, hosts.Channels.Select(c => c.Paired));
        Assert.Equal(2, hosts.Current);
        Assert.Single(device.Sent);
    }

    [Fact]
    public async Task A_silent_device_has_no_channels() =>
        Assert.Null(await HidppHosts.ReadAsync(new FakeDevice().Request, Direct, ChangeHost, HostsInfo, Timeout, CancellationToken.None));

    [Fact]
    public async Task A_device_that_stops_answering_after_the_switch_has_switched()
    {
        bool gone = false;
        var device = new FakeDevice
        {
            Answer = (feature, function, _) =>
            {
                if (feature == ChangeHost && function == 1) { gone = true; return null; }   // setCurrentHost never answers
                return gone ? null : Reply(ChangeHost, 0, 0x03, 0x00);
            },
        };
        Assert.Equal(SwitchResult.Switched, await HidppHosts.SwitchAsync(device.Request, Direct, ChangeHost, 2, NoDelay, CancellationToken.None));
        Assert.Equal(2, device.Sent.Count);   // one switch, one "still here?" check
    }

    [Fact]
    public async Task A_device_that_keeps_answering_failed_after_four_attempts()
    {
        var device = new FakeDevice { Answer = (f, fn, _) => fn == 0 ? Reply(ChangeHost, 0, 0x03, 0x00) : null };
        Assert.Equal(SwitchResult.Failed, await HidppHosts.SwitchAsync(device.Request, Direct, ChangeHost, 2, NoDelay, CancellationToken.None));

        var switches = device.Sent.Where(f => f[3] >> 4 == 1).ToList();
        Assert.Equal(HidppHosts.SwitchAttempts, switches.Count);
        Assert.All(switches, f => Assert.Equal(0x01, f[4]));
    }
}
