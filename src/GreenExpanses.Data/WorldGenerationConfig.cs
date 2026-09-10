using System.Reflection;
using System.Text.Json;
using GreenExpanses.Domain;

namespace GreenExpanses.Data;

public sealed record GenerationRangeBand(decimal Min, decimal Max, int Count, bool IncludeUpperBound);
public sealed record GenerationWeightBand(decimal Min, decimal Max, int WeightPercent, bool IncludeUpperBound);
public sealed record PhSigmaBand(decimal QualityMin, decimal Sigma);
public sealed record DrainageWeight(CatalogId Id, int WeightPercent);
public sealed record ScalarNoiseConfig(decimal NoiseMin, decimal NoiseMax, decimal Min, decimal Max);
public sealed record PhGenerationConfig(decimal Mean, decimal Min, decimal Max, IReadOnlyList<PhSigmaBand> SigmaBands);
public sealed record SoilGenerationConfig(
    decimal NBase, decimal NQualitySlope, decimal NManagementSlope, decimal NNoise,
    decimal PBase, decimal PQualitySlope, decimal PManagementSlope, decimal PNoise,
    decimal KBase, decimal KQualitySlope, decimal KManagementSlope, decimal KNoise,
    decimal SoilMin, decimal SoilMax,
    decimal OrganicMatterQualitySlope, decimal OrganicMatterManagementSlope, decimal OrganicMatterNoise,
    decimal OrganicMatterMin, decimal OrganicMatterMax);
public sealed record AgronomyGenerationConfig(
    ScalarNoiseConfig Fertility,
    ScalarNoiseConfig ManagementHistory,
    PhGenerationConfig Ph,
    IReadOnlyList<DrainageWeight> DrainageWeights,
    SoilGenerationConfig Soil);

public sealed record WorldGenerationConfig(
    CatalogId Id,
    int FieldCount,
    IReadOnlyList<GenerationRangeBand> AreaBands,
    IReadOnlyList<GenerationRangeBand> DistanceBands,
    IReadOnlyList<GenerationWeightBand> QualityBands,
    AgronomyGenerationConfig Agronomy);

public static class WorldGenerationConfigLoader
{
    public static WorldGenerationConfig Load(RuntimeConfig runtimeConfig)
    {
        ArgumentNullException.ThrowIfNull(runtimeConfig);
        var catalogId = new CatalogId("world_generation");
        if (!runtimeConfig.Catalogs.TryGetValue(catalogId, out var catalog))
        {
            throw new InvalidDataException("Required world_generation catalog is missing.");
        }

        return Parse(catalog.Root);
    }

    public static WorldGenerationConfig LoadBundled()
    {
        var assembly = typeof(WorldGenerationConfigLoader).Assembly;
        const string suffix = ".world_generation.json";
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(suffix, StringComparison.Ordinal))
            ?? throw new InvalidDataException("Bundled world_generation.json resource is missing.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException("Bundled world_generation.json resource could not be opened.");
        using var document = JsonDocument.Parse(stream);
        return Parse(document.RootElement);
    }

    private static WorldGenerationConfig Parse(JsonElement root)
    {
        var items = root.GetProperty("items");
        if (items.GetArrayLength() != 1)
        {
            throw new InvalidDataException("world_generation catalog must contain exactly one active baseline item.");
        }

        var item = items[0];
        var agronomy = item.GetProperty("agronomy");
        var ph = agronomy.GetProperty("ph");
        var soil = agronomy.GetProperty("soil");

        var config = new WorldGenerationConfig(
            new CatalogId(item.GetProperty("id").GetString()!),
            item.GetProperty("fieldCount").GetInt32(),
            ParseRangeBands(item.GetProperty("areaBands")),
            ParseRangeBands(item.GetProperty("distanceBands")),
            ParseWeightBands(item.GetProperty("qualityBands")),
            new AgronomyGenerationConfig(
                ParseScalarNoise(agronomy.GetProperty("fertility")),
                ParseScalarNoise(agronomy.GetProperty("managementHistory")),
                new PhGenerationConfig(
                    ph.GetProperty("mean").GetDecimal(),
                    ph.GetProperty("min").GetDecimal(),
                    ph.GetProperty("max").GetDecimal(),
                    ph.GetProperty("sigmaBands").EnumerateArray()
                        .Select(x => new PhSigmaBand(x.GetProperty("qualityMin").GetDecimal(), x.GetProperty("sigma").GetDecimal()))
                        .OrderByDescending(static x => x.QualityMin)
                        .ToArray()),
                agronomy.GetProperty("drainageWeights").EnumerateArray()
                    .Select(x => new DrainageWeight(new CatalogId(x.GetProperty("id").GetString()!), x.GetProperty("weightPercent").GetInt32()))
                    .ToArray(),
                new SoilGenerationConfig(
                    soil.GetProperty("nBase").GetDecimal(), soil.GetProperty("nQualitySlope").GetDecimal(), soil.GetProperty("nManagementSlope").GetDecimal(), soil.GetProperty("nNoise").GetDecimal(),
                    soil.GetProperty("pBase").GetDecimal(), soil.GetProperty("pQualitySlope").GetDecimal(), soil.GetProperty("pManagementSlope").GetDecimal(), soil.GetProperty("pNoise").GetDecimal(),
                    soil.GetProperty("kBase").GetDecimal(), soil.GetProperty("kQualitySlope").GetDecimal(), soil.GetProperty("kManagementSlope").GetDecimal(), soil.GetProperty("kNoise").GetDecimal(),
                    soil.GetProperty("soilMin").GetDecimal(), soil.GetProperty("soilMax").GetDecimal(),
                    soil.GetProperty("organicMatterQualitySlope").GetDecimal(), soil.GetProperty("organicMatterManagementSlope").GetDecimal(), soil.GetProperty("organicMatterNoise").GetDecimal(),
                    soil.GetProperty("organicMatterMin").GetDecimal(), soil.GetProperty("organicMatterMax").GetDecimal())));

        Validate(config);
        return config;
    }

    private static ScalarNoiseConfig ParseScalarNoise(JsonElement item) => new(
        item.GetProperty("noiseMin").GetDecimal(),
        item.GetProperty("noiseMax").GetDecimal(),
        item.GetProperty("min").GetDecimal(),
        item.GetProperty("max").GetDecimal());

    private static IReadOnlyList<GenerationRangeBand> ParseRangeBands(JsonElement array) =>
        array.EnumerateArray().Select(item => new GenerationRangeBand(
            item.GetProperty("min").GetDecimal(), item.GetProperty("max").GetDecimal(),
            item.GetProperty("count").GetInt32(), item.GetProperty("includeUpperBound").GetBoolean())).ToArray();

    private static IReadOnlyList<GenerationWeightBand> ParseWeightBands(JsonElement array) =>
        array.EnumerateArray().Select(item => new GenerationWeightBand(
            item.GetProperty("min").GetDecimal(), item.GetProperty("max").GetDecimal(),
            item.GetProperty("weightPercent").GetInt32(), item.GetProperty("includeUpperBound").GetBoolean())).ToArray();

    private static void Validate(WorldGenerationConfig config)
    {
        if (config.FieldCount <= 0) throw new InvalidDataException("fieldCount must be positive.");
        if (config.AreaBands.Sum(static x => x.Count) != config.FieldCount || config.DistanceBands.Sum(static x => x.Count) != config.FieldCount)
            throw new InvalidDataException("Area and distance band counts must each sum to fieldCount.");
        if (config.QualityBands.Sum(static x => x.WeightPercent) != 100) throw new InvalidDataException("Quality band weights must sum to 100%.");
        if (config.Agronomy.DrainageWeights.Sum(static x => x.WeightPercent) != 100) throw new InvalidDataException("Drainage weights must sum to 100%.");
        if (config.Agronomy.Ph.SigmaBands.Count == 0) throw new InvalidDataException("At least one pH sigma band is required.");
    }
}
