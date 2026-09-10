using System.Reflection;
using System.Text.Json;

namespace GreenExpanses.GodotClient;

internal sealed record FirstPlayableProfile(
    decimal StarterFieldTargetAreaHa,
    decimal StarterFieldMaxDistanceKm,
    decimal StartingCashRub)
{
    public static FirstPlayableProfile Default { get; } = new(30m, 10m, 1_000_000m);

    public static FirstPlayableProfile Load()
    {
        var assembly = typeof(FirstPlayableProfile).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(".first_playable.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException("Embedded first_playable.json is missing.");
        using var document = JsonDocument.Parse(stream);
        var item = document.RootElement.GetProperty("items")[0];
        return new FirstPlayableProfile(
            item.GetProperty("starterFieldTargetAreaHa").GetDecimal(),
            item.GetProperty("starterFieldMaxDistanceKm").GetDecimal(),
            item.GetProperty("startingCashRub").GetDecimal());
    }
}
