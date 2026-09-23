using BatteryBuddy.Core.Devices;
using BatteryBuddy.Core.Pnp;

namespace BatteryBuddy.Core.Tests;

public class PnpNodeMergerTests
{
    static PnpNode Le(string mac, string name, int? battery, bool connected) =>
        new($@"BTHLE\DEV_{mac}\7&21716ED6&0&{mac}", name, battery, connected, null);

    static PnpNode Classic(string mac, string name, bool connected) =>
        new($@"BTHENUM\DEV_{mac}\7&ABC256E&0&BLUETOOTHDEVICE_{mac}", name, null, connected, null);

    static PnpNode HandsFree(string mac, string name, int battery, string profile = "111E") =>
        new($@"BTHENUM\{{0000{profile}-0000-1000-8000-00805F9B34FB}}_HCIBYPASS_VID&00010075_PID&A013\7&ABC256E&0&{mac}_C00000000",
            name, battery, null, null);

    // The node set found by the 2026-09-23 scan.
    static readonly PnpNode[] Scan =
    {
        Le("DF43C7BC3A2B", "MX Master 3S", 55, true),
        Le("FF03000652C0", "RK-S98RGB", 21, true),
        Le("408E2C2B8126", "Xbox Wireless Controller", 75, false),
        Le("7F0383EC8468", "Buds3 Pro de Roberto", 100, false),
        Le("A0562CB7C045", "Buds3 Pro de Roberto", 98, false),
        Classic("A0562CAC1879", "Buds3 Pro de Roberto", true),
        HandsFree("A0562CAC1879", "Buds3 Pro de Roberto Hands-Free AG", 100),
        Classic("3053C1569598", "WF-C500", false),
        HandsFree("3053C1569598", "WF-C500 Hands-Free AG", 50),
        Classic("8CC5D0B7A749", "S25 Ultra de Roberto", false),
        Le("8CC5D0B7A749", "S25 Ultra de Roberto", null, false),
        HandsFree("8CC5D0B7A749", "S25 Ultra de Roberto Hands-Free HF", 100, "111F"),
        Classic("28D0EA0AF10D", "QUE5-L10063", false),
        Le("28D0EA0AF10D", "QUE5-L10063", null, false),
        new(@"BTH\MS_RFCOMM\6&22401E05&0&0", "Bluetooth Device (RFCOMM Protocol TDI)", null, true, null),
    };

    static IReadOnlyDictionary<string, DeviceReading> MergeScan() =>
        PnpNodeMerger.Merge(Scan, TestReadings.T0).ToDictionary(r => r.Name);

    [Fact]
    public void Merge_reports_one_reading_per_device_with_battery()
    {
        var names = MergeScan().Keys.OrderBy(n => n);
        Assert.Equal(
            new[] { "Buds3 Pro de Roberto", "MX Master 3S", "RK-S98RGB", "S25 Ultra de Roberto", "WF-C500", "Xbox Wireless Controller" },
            names);
    }

    [Fact]
    public void Merge_reads_le_devices()
    {
        var mouse = MergeScan()["MX Master 3S"];
        Assert.True(mouse.IsConnected);
        Assert.Equal(55, mouse.BatteryPercent);
        Assert.Equal(DeviceKind.Mouse, mouse.Kind);
        Assert.Equal(PnpNodeMerger.SourceName, mouse.Source);
    }

    [Fact]
    public void Hands_free_connection_comes_from_classic_node()
    {
        var merged = MergeScan();
        Assert.True(merged["Buds3 Pro de Roberto"].IsConnected);
        Assert.False(merged["WF-C500"].IsConnected);
        Assert.Equal(50, merged["WF-C500"].BatteryPercent);
    }

    [Fact]
    public void Merge_uses_connected_node_not_stale_le_value()
    {
        var nodes = new[]
        {
            Le("7F0383EC8468", "Buds3 Pro de Roberto", 100, false),
            Classic("A0562CAC1879", "Buds3 Pro de Roberto", true),
            HandsFree("A0562CAC1879", "Buds3 Pro de Roberto Hands-Free AG", 70),
        };
        var buds = Assert.Single(PnpNodeMerger.Merge(nodes, TestReadings.T0));
        Assert.Equal(70, buds.BatteryPercent);
        Assert.Equal(DeviceKind.Earbuds, buds.Kind);
    }

    [Fact]
    public void Connected_device_never_shows_a_stale_sibling_battery()
    {
        // Right after connecting, the Hands-Free node may not carry a battery yet; the stale LE 100 must not leak.
        var nodes = new[]
        {
            Le("7F0383EC8468", "Buds3 Pro de Roberto", 100, false),
            Classic("A0562CAC1879", "Buds3 Pro de Roberto", true),
        };
        var buds = Assert.Single(PnpNodeMerger.Merge(nodes, TestReadings.T0));
        Assert.True(buds.IsConnected);
        Assert.Null(buds.BatteryPercent);
    }

    [Fact]
    public void Hands_free_suffix_is_stripped_for_phone()
    {
        var phone = MergeScan()["S25 Ultra de Roberto"];
        Assert.Equal(DeviceKind.Phone, phone.Kind);
        Assert.Equal(DeviceKey.ForName("S25 Ultra de Roberto"), phone.Key);
    }
}
