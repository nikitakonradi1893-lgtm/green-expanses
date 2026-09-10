using GreenExpanses.Domain;
using GreenExpanses.Persistence;
using GreenExpanses.Simulation;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class AgronomicBaselineTests
{
    [Fact]
    public void GeneratedFieldsRespectApprovedAgronomicRanges()
    {
        var fields = GameStateFactory.Create(20260910UL).World.Fields;

        Assert.All(fields, field =>
        {
            Assert.InRange(field.Fertility, 45m, 100m);
            Assert.InRange(field.Ph, 5m, 7.8m);
            Assert.InRange(field.ManagementHistory, 20m, 100m);
            Assert.InRange(field.SoilN, 5m, 95m);
            Assert.InRange(field.SoilP, 5m, 95m);
            Assert.InRange(field.SoilK, 5m, 95m);
            Assert.InRange(field.OrganicMatter, 35m, 100m);
        });
    }

    [Fact]
    public void DrainageDistributionConvergesToApprovedWeightsAcrossSeeds()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        const int seedCount = 40;

        foreach (var seed in Enumerable.Range(1, seedCount).Select(static value => (ulong)value))
        {
            foreach (var field in GameStateFactory.Create(seed).World.Fields)
            {
                counts[field.DrainageType.Value] = counts.GetValueOrDefault(field.DrainageType.Value) + 1;
            }
        }

        var total = seedCount * WorldGenerator.FieldCount;
        AssertWeight(counts, "normal", total, 0.70m, 0.02m);
        AssertWeight(counts, "dry_prone", total, 0.15m, 0.015m);
        AssertWeight(counts, "wet_prone", total, 0.12m, 0.015m);
        AssertWeight(counts, "problematic", total, 0.03m, 0.01m);
    }

    [Fact]
    public void SameSeedProducesSameAgronomicSignature()
    {
        var first = GameStateFactory.Create(77UL).World.Fields;
        var second = GameStateFactory.Create(77UL).World.Fields;

        Assert.Equal(Signature(first), Signature(second));
    }

    [Fact]
    public void SaveRoundTripPreservesAgronomicBaseline()
    {
        var state = GameStateFactory.Create(20260910UL);
        var timestamp = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var json = SaveV1Serializer.Serialize(
            state,
            new ConfigVersion("1.0.0-dev"),
            "world-002-test",
            timestamp,
            timestamp);

        var loaded = SaveV1Serializer.Deserialize(json).State;

        Assert.Equal(Signature(state.World.Fields), Signature(loaded.World.Fields));
    }

    private static void AssertWeight(
        IReadOnlyDictionary<string, int> counts,
        string id,
        int total,
        decimal expected,
        decimal tolerance)
    {
        var actual = (decimal)counts.GetValueOrDefault(id) / total;
        Assert.InRange(actual, expected - tolerance, expected + tolerance);
    }

    private static string Signature(IEnumerable<Field> fields) => string.Join('|', fields.Select(static field =>
        $"{field.Id}:{field.Fertility:F2}:{field.Ph:F2}:{field.ManagementHistory:F2}:{field.DrainageType}:{field.SoilN:F2}:{field.SoilP:F2}:{field.SoilK:F2}:{field.OrganicMatter:F2}"));
}
