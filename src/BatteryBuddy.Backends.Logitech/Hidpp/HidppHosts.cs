using BatteryBuddy.Devices;

namespace BatteryBuddy.Backends.Logitech.Hidpp;

/// <summary>Sends a request and returns its reply, or null on timeout, error reply or a dead channel (see HidChannel.RequestAsync).</summary>
public delegate Task<byte[]?> HidppRequest(byte[] frame, TimeSpan timeout, CancellationToken ct);

/// <summary>
/// CHANGE_HOST (0x1814) and HOSTS_INFO (0x1815): a device's Easy-Switch channels, which are paired, and moving it
/// to another. Hosts are 0-based on the wire and 1-based (as printed on the mouse) everywhere else.
/// </summary>
public static class HidppHosts
{
    /// <summary>How long to let the device drop after a switch, and to wait for the "still here?" answer.</summary>
    public static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(250);
    static readonly TimeSpan RetryGap = TimeSpan.FromMilliseconds(200);
    public const int SwitchAttempts = 4;
    const byte Paired = 0x01;

    public static byte[] GetHostInfo(byte deviceIndex, byte changeHost) => HidppProtocol.Request(deviceIndex, changeHost, 0);

    public static byte[] SetCurrentHost(byte deviceIndex, byte changeHost, int channel) =>
        HidppProtocol.Request(deviceIndex, changeHost, 1, (byte)(channel - 1));

    public static byte[] GetHostStatus(byte deviceIndex, byte hostsInfo, int channel) =>
        HidppProtocol.Request(deviceIndex, hostsInfo, 1, (byte)(channel - 1));

    /// <summary>CHANGE_HOST getHostInfo params [count, current, …], every channel taken as paired. Null if malformed.</summary>
    public static HostChannels? ParseHostInfo(ReadOnlySpan<byte> p)
    {
        if (p.Length < 2 || p[0] == 0 || p[1] >= p[0]) return null;
        var channels = Enumerable.Range(1, p[0]).Select(n => new HostChannel(n, true)).ToList();
        return new HostChannels(channels, p[1] + 1);
    }

    /// <summary>HOSTS_INFO getHostInfo params [host, status, bus, pages, nameLen, nameMax]: paired or not; null if it isn't about this channel.</summary>
    public static bool? ParsePaired(ReadOnlySpan<byte> p, int channel) =>
        p.Length < 2 || p[0] != channel - 1 ? null : p[1] == Paired;

    /// <summary>Channel count and current channel; with HOSTS_INFO (index not 0), which of the others are paired.</summary>
    public static async Task<HostChannels?> ReadAsync(HidppRequest request, byte deviceIndex, byte changeHost, byte hostsInfo,
        TimeSpan timeout, CancellationToken ct)
    {
        var info = await request(GetHostInfo(deviceIndex, changeHost), timeout, ct);
        if (info is null || ParseHostInfo(HidppProtocol.Params(info)) is not { } hosts) return null;
        if (hostsInfo == 0) return hosts;

        var channels = new List<HostChannel>();
        foreach (var channel in hosts.Channels)
        {
            // The current channel is paired by definition. A status we can't read leaves a channel pickable,
            // as on a mouse without HOSTS_INFO.
            if (channel.Number == hosts.Current)
            {
                channels.Add(channel);
                continue;
            }
            var status = await request(GetHostStatus(deviceIndex, hostsInfo, channel.Number), timeout, ct);
            bool paired = status is null || (ParsePaired(HidppProtocol.Params(status), channel.Number) ?? true);
            channels.Add(channel with { Paired = paired });
        }
        return hosts with { Channels = channels };
    }

    /// <summary>
    /// setCurrentHost never answers: the device drops the link as it switches. So verify by absence, as
    /// mxswitch.ps1 does: after each send ask getHostInfo, and no answer means it left. Still answering after
    /// every attempt means the frame didn't land or the device refused.
    /// </summary>
    public static async Task<SwitchResult> SwitchAsync(HidppRequest request, byte deviceIndex, byte changeHost, int channel,
        Func<TimeSpan, CancellationToken, Task> delay, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= SwitchAttempts; attempt++)
        {
            await request(SetCurrentHost(deviceIndex, changeHost, channel), SettleTime, ct);   // no reply comes: this is the settle wait
            if (await request(GetHostInfo(deviceIndex, changeHost), SettleTime, ct) is null) return SwitchResult.Switched;
            if (attempt < SwitchAttempts) await delay(RetryGap, ct);
        }
        return SwitchResult.Failed;
    }
}
