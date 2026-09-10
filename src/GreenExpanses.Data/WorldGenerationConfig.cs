using System.Reflection;
using System.Text.Json;
using GreenExpanses.Domain;

namespace GreenExpanses.Data;

public sealed record GenerationRangeBand(
    decimal Min,
    decimal Max,
    int Count,
    bool IncludeUpperBound);

public sealed record GenerationWeightBand(
    decimal Min,
    decimal Max,
    int WeightPercent,
    bool IncludeUpperBound);

public sealed record WorldGenerationConfig(
    CatalogId Id,
    int FieldCount,
    IReadOnlyList<GenerationRangeBand> AreaBands,
    IReadOnlyList<GenerationRangeBand> DistanceBands,
    IReadOnlyList<GenerationWeightBand> QualityBands);

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
        var config = new WorldGenerationConfig(
            new CatalogId(item.GetProperty("id").GetString()!),
            item.GetProperty("fieldCount").GetInt32(),
            ParseRangeBands(item.GetProperty("areaBands")),
            ParseRangeBands(item.GetProperty("distanceBands")),
            ParseWeightBands(item.GetProperty("qualityBands")));

        Validate(config);
        return config;
    }

    private static IReadOnlyList<GenerationRangeBand> ParseRangeBands(JsonElement array) =>
        array.EnumerateArray()
            .Select(item => new GenerationRangeBand(
                item.GetProperty("min").GetDecimal(),
                item.GetProperty("max").GetDecimal(),
                item.GetProperty("count").GetInt32(),
                item.GetProperty("includeUpperBound").GetBoolean()))
            .ToArray();

    private static IReadOnlyList<GenerationWeightBand> ParseWeightBands(JsonElement array) =>
        array.EnumerateArray()
            .Select(item => new GenerationWeightBand(
                item.GetProperty("min").GetDecimal(),
                item.GetProperty("max").GetDecimal(),
                item.GetProperty("weightPercent").GetInt32(),
                item.GetProperty("includeUpperBound").GetBoolean()))
            .ToArray();

    private static void Validate(WorldGenerationConfig config)
    {
        if (config.FieldCount <= 0)
        {
            throw new InvalidDataException("fieldCount must be positive.");
        }

        if (config.AreaBands.Sum(static band => band.Count) != config.FieldCount ||
            config.DistanceBands.Sum(static band => band.Count) != config.FieldCount)
        {
            throw new InvalidDataException("Area and distance band counts must each sum to fieldCount.");
        }

        if (config.QualityBands.Sum(static band => band.WeightPercent) != 100)
        {
            throw new InvalidDataException("Quality band weights must sum to 100%.");
        }

        if (config.AreaBands.Any(static band => band.Min >= band.Max || band.Count <= 0) ||
            config.DistanceBands.Any(static band => band.Min >= band.Max || band.Count <= 0) ||
            config.QualityBands.Any(static band => band.Min >= band.Max || band.WeightPercent <= 0))
        {
            throw new InvalidDataException("World generation bands contain invalid ranges or weights.");
        }
    }
}
