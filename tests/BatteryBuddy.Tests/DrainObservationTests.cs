using BatteryBuddy.Backend;
using BatteryBuddy.Devices;

namespace BatteryBuddy.Tests;

/// <summary>What the registry feeds the drain history from merged readings.</summary>
public class DrainObservationTests
{
    readonly DrainHistory _drain = new();
    readonly DeviceRegistry _registry;

    public DrainObservationTests() => _registry = new DeviceRegistry(drain: _drain);

    static DeviceReading Mouse(int battery, int minutes, bool connected = true) =>
        TestReadings.Make("MX Master 3S", DeviceKind.Mouse, connected, battery, at: TestReadings.T0.AddMinutes(minutes), source: "logitech")
            with { ChargingKnown = true };

    static DeviceReading Buds(int left, int minutes, bool worn = true, bool inCase = false) =>
        TestReadings.Make("Buds3 Pro de Roberto", DeviceKind.Earbuds,
            detail: new BudsDetail(left, 90, 50, worn, worn, inCase, false), at: TestReadings.T0.AddMinutes(minutes), source: "galaxybuds");

    void Apply(DeviceReading reading) => _registry.Apply(reading.Source, new[] { reading });

    [Fact]
    public void Draining_readings_become_segments()
    {
        Apply(Mouse(60, 0));
        Apply(Mouse(59, 10));
        Apply(Mouse(58, 70));
        Assert.Single(_drain.Segments(Mouse(0, 0).Key));
    }

    [Fact]
    public void A_disconnect_ends_the_stretch()
    {
        Apply(Mouse(60, 0));
        Apply(Mouse(59, 10));
        Apply(Mouse(59, 20, connected: false));
        Apply(Mouse(57, 90));
        Apply(Mouse(56, 100));
        Assert.Empty(_drain.Segments(Mouse(0, 0).Key));
    }

    [Fact]
    public void Charging_is_not_measured()
    {
        Apply(Mouse(60, 0) with { IsCharging = true });
        Apply(Mouse(59, 10) with { IsCharging = true });
        Apply(Mouse(58, 20) with { IsCharging = true });
        Assert.Empty(_drain.Segments(Mouse(0, 0).Key));
    }

    [Fact]
    public void Buds_count_only_while_worn_and_out_of_the_case()
    {
        var key = Buds(0, 0).Key;
        Apply(Buds(80, 0, worn: false));
        Apply(Buds(79, 5, worn: false));
        Apply(Buds(78, 10, worn: false));
        Apply(Buds(77, 15, inCase: true));
        Apply(Buds(76, 20, inCase: true));
        Assert.Empty(_drain.Segments(key));

        Apply(Buds(76, 25));
        Apply(Buds(75, 30));
        Apply(Buds(74, 35));
        Assert.Equal(new DrainSegment(TestReadings.T0.AddMinutes(30), TestReadings.T0.AddMinutes(35), 75, 74),
            Assert.Single(_drain.Segments(key)));
    }
}
