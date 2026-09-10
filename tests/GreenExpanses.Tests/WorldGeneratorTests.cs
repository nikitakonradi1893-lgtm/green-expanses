using GreenExpanses.Domain;
using GreenExpanses.Persistence;
using GreenExpanses.Simulation;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class WorldGeneratorTests
{
    [Fact]
    public void NewCampaignCreatesExactly500UniqueFields()
    {
        var state = GameStateFactory.Create(20260910UL);

        Assert.Equal(500, state.World.Fields.Count);
        Assert.Equal(500, state.World.FieldRegistry.Count);
        Assert.Equal(500, state.World.Fields.Select(static field => field.Id).Distinct().Count());
    }

    [Fact]
    public void AreaAndDistanceUseApprovedExactClassCounts()
    {
        var fields = GameStateFactory.Create(20260910UL).World.Fields;

        Assert.Equal(new[] { 75, 125, 150, 100, 40, 10 }, CountBands(fields.Select(static field => field.Area.Value),
            (5m, 25m), (25m, 60m), (60m, 120m), (120m, 200m), (200m, 300m), (300m, 400.01m)));

        Assert.Equal(new[] { 75, 125, 150, 100, 40, 10 }, CountBands(fields.Select(static field => field.DistanceKm),
            (0.5m, 5m), (5m, 10m), (10m, 20m), (20m, 30m), (30m, 45m), (45m, 60.01m)));
    }

    [Fact]
    public void QualityDistributionConvergesToApprovedWeightsAcrossSeeds()
    {
        var counts = new int[5];
        const int seedCount = 40;
        foreach (var seed in Enumerable.Range(1, seedCount).Select(static value => (ulong)value))
        {
            foreach (var field in GameStateFactory.Create(seed).World.Fields)
            {
                var q = field.FieldQualityBase;
                var index = q < 55m ? 0 : q < 70m ? 1 : q < 80m ? 2 : q < 90m ? 3 : 4;
                counts[index]++;
            }
        }

        var total = seedCount * WorldGenerator.FieldCount;
        var expected = new[] { 0.10m, 0.25m, 0.35m, 0.20m, 0.10m };
        for (var i = 0; i < counts.Length; i++)
        {
            var actual = (decimal)counts[i] / total;
            Assert.InRange(actual, expected[i] - 0.02m, expected[i] + 0.02m);
        }
    }

    [Fact]
    public void SameSeedProducesSameFieldSequenceAndDifferentSeedChangesWorld()
    {
        var first = GameStateFactory.Create(77UL).World.Fields;
        var second = GameStateFactory.Create(77UL).World.Fields;
        var different = GameStateFactory.Create(78UL).World.Fields;

        Assert.Equal(FieldSignature(first), FieldSignature(second));
        Assert.NotEqual(FieldSignature(first), FieldSignature(different));
    }

    [Fact]
    public void SaveRoundTripPreservesGeneratedFieldValues()
    {
        var state = GameStateFactory.Create(20260910UL);
        var json = SaveV1Serializer.Serialize(
            state,
            new ConfigVersion("1.0.0-dev"),
            "world-001-test",
            new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc));

        var loaded = SaveV1Serializer.Deserialize(json).State;

        Assert.Equal(500, loaded.World.Fields.Count);
        Assert.Equal(FieldSignature(state.World.Fields), FieldSignature(loaded.World.Fields));
    }

    private static int[] CountBands(IEnumerable<decimal> values, params (decimal Min, decimal MaxExclusive)[] bands)
    {
        var counts = new int[bands.Length];
        foreach (var value in values)
        {
            for (var i = 0; i < bands.Length; i++)
            {
                if (value >= bands[i].Min && value < bands[i].MaxExclusive)
                {
                    counts[i]++;
                    break;
                }
            }
        }

        return counts;
    }

    private static string FieldSignature(IEnumerable<Field> fields) => string.Join('|', fields
        .OrderBy(static field => field.Id.Value)
        .Select(static field => $"{field.Id}:{field.Area.Value:F2}:{field.DistanceKm:F2}:{field.FieldQualityBase:F2}"));
}
