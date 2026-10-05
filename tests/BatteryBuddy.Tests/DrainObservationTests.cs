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

public class OneBudDrainTests
{
    readonly DrainHistory _drain = new();
    readonly DeviceRegistry _registry;

    public OneBudDrainTests() => _registry = new DeviceRegistry(drain: _drain);

    // Right bud in the ear, left charging in the case.
    static DeviceReading Buds(int left, int right, int minutes, bool leftInCase = true) =>
        TestReadings.Make("Buds3 Pro de Roberto", DeviceKind.Earbuds, at: TestReadings.T0.AddMinutes(minutes), source: "galaxy-buds",
            detail: new BudsDetail(left, right, 50, false, true, LeftInCase: leftInCase));

    void Apply(DeviceReading reading) => _registry.Apply(reading.Source, new[] { reading });

    [Fact]
    public void The_bud_in_the_ear_is_measured_while_its_partner_charges()
    {
        Apply(Buds(40, 80, 0));
        Apply(Buds(42, 79, 10));
        Apply(Buds(44, 78, 20));
        Assert.Equal(new DrainSegment(TestReadings.T0.AddMinutes(10), TestReadings.T0.AddMinutes(20), 79, 78),
            Assert.Single(_drain.Segments(Buds(0, 0, 0).Key)));
    }

    [Fact]
    public void Taking_the_other_bud_out_starts_a_new_stretch()
    {
        static DeviceReading BothOut(int left, int right, int minutes) =>
            Buds(left, right, minutes, leftInCase: false) with { Detail = new BudsDetail(left, right, 50, true, true) };
        Apply(Buds(70, 80, 0));
        Apply(Buds(70, 79, 10));
        Apply(Buds(70, 78, 20));
        Apply(BothOut(70, 78, 30));   // now the lower of both: 70, not a drop from 78
        Apply(BothOut(69, 78, 40));
        Assert.Equal(new DrainSegment(TestReadings.T0.AddMinutes(10), TestReadings.T0.AddMinutes(20), 79, 78),
            Assert.Single(_drain.Segments(Buds(0, 0, 0).Key)));
    }
}
