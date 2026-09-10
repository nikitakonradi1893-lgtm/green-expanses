using System.Text.Json;
using GreenExpanses.Domain;

namespace GreenExpanses.Persistence;

public static class CanonicalStateSnapshot
{
    private static readonly DateTime FixedTimestamp = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static string Serialize(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var envelope = SaveV1Serializer.Serialize(
            state,
            new ConfigVersion("fixture"),
            "fixture",
            FixedTimestamp,
            FixedTimestamp);

        using var document = JsonDocument.Parse(envelope);
        return document.RootElement.GetProperty("state").GetRawText();
    }
}
