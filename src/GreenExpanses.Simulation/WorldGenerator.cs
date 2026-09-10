using GreenExpanses.Domain;
using GreenExpanses.Infrastructure;

namespace GreenExpanses.Simulation;

public sealed record WorldGenerationResult(WorldState World, RngState RngState);

public static class WorldGenerator
{
    public const int GeneratorVersion = 1;
    public const int FieldCount = 500;

    private static readonly RangeBand[] AreaBands =
    [
        new(5m, 25m, 75),
        new(25m, 60m, 125),
        new(60m, 120m, 150),
        new(120m, 200m, 100),
        new(200m, 300m, 40),
        new(300m, 400m, 10, IncludeUpperBound: true)
    ];

    private static readonly RangeBand[] DistanceBands =
    [
        new(0.5m, 5m, 75),
        new(5m, 10m, 125),
        new(10m, 20m, 150),
        new(20m, 30m, 100),
        new(30m, 45m, 40),
        new(45m, 60m, 10, IncludeUpperBound: true)
    ];

    private static readonly QualityBand[] QualityBands =
    [
        new(40m, 55m, 10),
        new(55m, 70m, 25),
        new(70m, 80m, 35),
        new(80m, 90m, 20),
        new(90m, 100m, 10, IncludeUpperBound: true)
    ];

    public static WorldGenerationResult Generate(ulong worldSeed, RngState initialRngState)
    {
        ArgumentNullException.ThrowIfNull(initialRngState);

        var rng = new DeterministicRngService(worldSeed, initialRngState);
        var areas = GenerateExactDistribution(AreaBands, rng);
        var distances = GenerateExactDistribution(DistanceBands, rng);
        Shuffle(areas, rng, RngStreams.Land);
        Shuffle(distances, rng, RngStreams.Land);

        var world = new WorldState();
        for (var index = 0; index < FieldCount; index++)
        {
            world.AddField(new Field
            {
                Id = new EntityId(DeterministicFieldGuid(worldSeed, index)),
                Area = new AreaHa(areas[index]),
                DistanceKm = distances[index],
                FieldQualityBase = GenerateQuality(rng)
            });
        }

        return new WorldGenerationResult(world, rng.Snapshot());
    }

    private static List<decimal> GenerateExactDistribution(
        IReadOnlyList<RangeBand> bands,
        IDeterministicRng rng)
    {
        var result = new List<decimal>(FieldCount);
        foreach (var band in bands)
        {
            for (var i = 0; i < band.Count; i++)
            {
                result.Add(NextHundredth(rng, RngStreams.Land, band.Min, band.Max, band.IncludeUpperBound));
            }
        }

        if (result.Count != FieldCount)
        {
            throw new InvalidOperationException($"World distribution defines {result.Count} fields instead of {FieldCount}.");
        }

        return result;
    }

    private static decimal GenerateQuality(IDeterministicRng rng)
    {
        var roll = rng.NextInt(RngStreams.Quality, 0, 100);
        var cumulative = 0;
        foreach (var band in QualityBands)
        {
            cumulative += band.WeightPercent;
            if (roll < cumulative)
            {
                return NextHundredth(rng, RngStreams.Quality, band.Min, band.Max, band.IncludeUpperBound);
            }
        }

        throw new InvalidOperationException("FieldQualityBase probability bands do not sum to 100%.");
    }

    private static decimal NextHundredth(
        IDeterministicRng rng,
        string stream,
        decimal min,
        decimal max,
        bool includeUpperBound)
    {
        var minHundredths = checked((int)(min * 100m));
        var maxHundredths = checked((int)(max * 100m));
        var exclusiveMax = includeUpperBound ? checked(maxHundredths + 1) : maxHundredths;
        return rng.NextInt(stream, minHundredths, exclusiveMax) / 100m;
    }

    private static void Shuffle<T>(IList<T> values, IDeterministicRng rng, string stream)
    {
        for (var i = values.Count - 1; i > 0; i--)
        {
            var j = rng.NextInt(stream, 0, i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private static Guid DeterministicFieldGuid(ulong worldSeed, int index)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes[..8], worldSeed);
        BitConverter.TryWriteBytes(bytes[8..12], index);
        BitConverter.TryWriteBytes(bytes[12..], 0x464C4431); // "FLD1"
        return new Guid(bytes);
    }

    private sealed record RangeBand(
        decimal Min,
        decimal Max,
        int Count,
        bool IncludeUpperBound = false);

    private sealed record QualityBand(
        decimal Min,
        decimal Max,
        int WeightPercent,
        bool IncludeUpperBound = false);
}
