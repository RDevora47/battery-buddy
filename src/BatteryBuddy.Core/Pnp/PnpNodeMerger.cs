using System.Text.RegularExpressions;
using BatteryBuddy.Core.Devices;

namespace BatteryBuddy.Core.Pnp;

public static class PnpNodeMerger
{
    public const string SourceName = "windows";

    enum NodeType { LowEnergy, ClassicMain, HandsFree }

    sealed record Parsed(PnpNode Node, NodeType Type, string Mac, string DisplayName);

    static readonly Regex LowEnergyId = new(@"^BTHLE\\DEV_([0-9A-F]{12})\\", RegexOptions.IgnoreCase);
    static readonly Regex ClassicId = new(@"^BTHENUM\\DEV_([0-9A-F]{12})\\", RegexOptions.IgnoreCase);
    static readonly Regex HandsFreeId = new(@"^BTHENUM\\\{0000111[EF]-0000-1000-8000-00805F9B34FB\}.*&([0-9A-F]{12})_C", RegexOptions.IgnoreCase);
    static readonly Regex HandsFreeSuffix = new(@"\s+Hands-Free(\s+(AG|HF))?$", RegexOptions.IgnoreCase);

    public static IReadOnlyList<DeviceReading> Merge(IEnumerable<PnpNode> nodes, DateTimeOffset now)
    {
        var parsed = nodes.Select(Parse).OfType<Parsed>().ToList();
        var classicConnected = parsed
            .Where(p => p.Type == NodeType.ClassicMain && p.Node.IsConnected == true)
            .Select(p => p.Mac)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool IsConnected(Parsed p) =>
            p.Type == NodeType.HandsFree ? classicConnected.Contains(p.Mac) : p.Node.IsConnected == true;

        var readings = new List<DeviceReading>();
        foreach (var group in parsed.GroupBy(p => DeviceKey.ForName(p.DisplayName)))
        {
            var withBattery = group.Where(p => p.Node.Battery is not null).ToList();
            if (withBattery.Count == 0) continue;

            bool connected = group.Any(IsConnected);
            var batteryNode = withBattery.FirstOrDefault(IsConnected) ?? withBattery[0];
            string name = group.First().DisplayName;
            uint? cod = group.Select(p => p.Node.ClassOfDevice).FirstOrDefault(c => c is not null);

            readings.Add(new DeviceReading(
                group.Key, name, DeviceKindClassifier.Classify(name, cod), connected,
                batteryNode.Node.Battery, null, now, SourceName));
        }
        return readings;
    }

    static Parsed? Parse(PnpNode node)
    {
        if (LowEnergyId.Match(node.InstanceId) is { Success: true } le)
            return new(node, NodeType.LowEnergy, le.Groups[1].Value, node.Name);
        if (ClassicId.Match(node.InstanceId) is { Success: true } classic)
            return new(node, NodeType.ClassicMain, classic.Groups[1].Value, node.Name);
        if (HandsFreeId.Match(node.InstanceId) is { Success: true } hf)
            return new(node, NodeType.HandsFree, hf.Groups[1].Value, HandsFreeSuffix.Replace(node.Name, ""));
        return null;
    }
}
