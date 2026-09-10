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
            var quality = GenerateQuality(config.QualityBands, rng);
            var management = GenerateScalarFromQuality(quality, config.Agronomy.ManagementHistory, rng);
            var soil = config.Agronomy.Soil;

            world.AddField(new Field
            {
                Id = new EntityId(DeterministicFieldGuid(worldSeed, index)),
                Area = new AreaHa(areas[index]),
                DistanceKm = distances[index],
                FieldQualityBase = quality,
                Fertility = GenerateScalarFromQuality(quality, config.Agronomy.Fertility, rng),
                Ph = GeneratePh(quality, config.Agronomy.Ph, rng),
                ManagementHistory = management,
                DrainageType = GenerateDrainage(config.Agronomy.DrainageWeights, rng),
                SoilN = GenerateSoilValue(soil.NBase, soil.NQualitySlope, soil.NManagementSlope, soil.NNoise, quality, management, soil.SoilMin, soil.SoilMax, rng),
                SoilP = GenerateSoilValue(soil.PBase, soil.PQualitySlope, soil.PManagementSlope, soil.PNoise, quality, management, soil.SoilMin, soil.SoilMax, rng),
                SoilK = GenerateSoilValue(soil.KBase, soil.KQualitySlope, soil.KManagementSlope, soil.KNoise, quality, management, soil.SoilMin, soil.SoilMax, rng),
                OrganicMatter = Clamp(
                    quality * soil.OrganicMatterQualitySlope +
                    Uniform(rng, -soil.OrganicMatterNoise, soil.OrganicMatterNoise) +
                    (management - 60m) * soil.OrganicMatterManagementSlope,
                    soil.OrganicMatterMin,
                    soil.OrganicMatterMax)
            });
        }

        return new WorldGenerationResult(world, rng.Snapshot());
    }

    private static decimal GenerateScalarFromQuality(decimal quality, ScalarNoiseConfig config, IDeterministicRng rng) =>
        Clamp(quality + Uniform(rng, config.NoiseMin, config.NoiseMax), config.Min, config.Max);

    private static decimal GenerateSoilValue(
        decimal baseValue,
        decimal qualitySlope,
        decimal managementSlope,
        decimal noise,
        decimal quality,
        decimal management,
        decimal min,
        decimal max,
        IDeterministicRng rng)
    {
        var baseline = baseValue + (quality - 70m) * qualitySlope;
        return Clamp(
            baseline + (management - 60m) * managementSlope + Uniform(rng, -noise, noise),
            min,
            max);
    }

    private static decimal GeneratePh(decimal quality, PhGenerationConfig config, IDeterministicRng rng)
    {
        var sigma = config.SigmaBands.FirstOrDefault(band => quality >= band.QualityMin)?.Sigma
            ?? config.SigmaBands[^1].Sigma;
        var u1 = Math.Max(rng.NextUnitDouble(RngStreams.Quality), double.Epsilon);
        var u2 = rng.NextUnitDouble(RngStreams.Quality);
        var standardNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        var value = config.Mean + (decimal)standardNormal * sigma;
        return decimal.Round(Clamp(value, config.Min, config.Max), 2, MidpointRounding.AwayFromZero);
    }

    private static CatalogId GenerateDrainage(IReadOnlyList<DrainageWeight> weights, IDeterministicRng rng)
    {
        var roll = rng.NextInt(RngStreams.Quality, 0, 100);
        var cumulative = 0;
        foreach (var weight in weights)
        {
            cumulative += weight.WeightPercent;
            if (roll < cumulative)
            {
                return weight.Id;
            }
        }

        throw new InvalidOperationException("Drainage weights do not sum to 100%.");
    }

    private static decimal Uniform(IDeterministicRng rng, decimal min, decimal max)
    {
        var unit = (decimal)rng.NextUnitDouble(RngStreams.Quality);
        return decimal.Round(min + (max - min) * unit, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal Clamp(decimal value, decimal min, decimal max) => Math.Min(max, Math.Max(min, value));

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

    private static decimal GenerateQuality(IReadOnlyList<GenerationWeightBand> bands, IDeterministicRng rng)
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

    private static decimal NextHundredth(IDeterministicRng rng, string stream, decimal min, decimal max, bool includeUpperBound)
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
        BitConverter.TryWriteBytes(bytes[12..], 0x464C4431);
        return new Guid(bytes);
    }
}
