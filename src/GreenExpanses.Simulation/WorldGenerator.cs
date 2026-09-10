using GreenExpanses.Data;
using GreenExpanses.Domain;
using GreenExpanses.Infrastructure;

namespace GreenExpanses.Simulation;

public sealed record WorldGenerationResult(WorldState World, RngState RngState);

public static class WorldGenerator
{
    public const int GeneratorVersion = 1;
    public static int FieldCount => WorldGenerationConfigLoader.LoadBundled().FieldCount;

    public static WorldGenerationResult Generate(
        ulong worldSeed,
        RngState initialRngState,
        WorldGenerationConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(initialRngState);
        config ??= WorldGenerationConfigLoader.LoadBundled();

        var rng = new DeterministicRngService(worldSeed, initialRngState);
        var areas = GenerateExactDistribution(config.AreaBands, config.FieldCount, rng);
        var distances = GenerateExactDistribution(config.DistanceBands, config.FieldCount, rng);
        Shuffle(areas, rng, RngStreams.Land);
        Shuffle(distances, rng, RngStreams.Land);

        var world = new WorldState();
        for (var index = 0; index < config.FieldCount; index++)
        {
            world.AddField(new Field
            {
                Id = new EntityId(DeterministicFieldGuid(worldSeed, index)),
                Area = new AreaHa(areas[index]),
                DistanceKm = distances[index],
                FieldQualityBase = GenerateQuality(config.QualityBands, rng)
            });
        }

        return new WorldGenerationResult(world, rng.Snapshot());
    }

    private static List<decimal> GenerateExactDistribution(
        IReadOnlyList<GenerationRangeBand> bands,
        int fieldCount,
        IDeterministicRng rng)
    {
        var result = new List<decimal>(fieldCount);
        foreach (var band in bands)
        {
            for (var i = 0; i < band.Count; i++)
            {
                result.Add(NextHundredth(rng, RngStreams.Land, band.Min, band.Max, band.IncludeUpperBound));
            }
        }

        if (result.Count != fieldCount)
        {
            throw new InvalidOperationException($"World distribution defines {result.Count} fields instead of {fieldCount}.");
        }

        return result;
    }

    private static decimal GenerateQuality(
        IReadOnlyList<GenerationWeightBand> bands,
        IDeterministicRng rng)
    {
        var roll = rng.NextInt(RngStreams.Quality, 0, 100);
        var cumulative = 0;
        foreach (var band in bands)
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
}
