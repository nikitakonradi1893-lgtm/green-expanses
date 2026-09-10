using System.Globalization;
using System.Text;
using GreenExpanses.Domain;
using GreenExpanses.Infrastructure;

namespace GreenExpanses.Simulation;

public static class GameStateFactory
{
    private static readonly GameDateTime CampaignEpoch = new(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));

    public static GameState Create(ulong worldSeed, string difficultyProfileId = "normal")
    {
        return new GameState
        {
            CampaignId = new EntityId(DeterministicGuid(worldSeed, 0xC0A1UL)),
            WorldSeed = worldSeed,
            CurrentDateTime = CampaignEpoch,
            DifficultyProfileId = new CatalogId(difficultyProfileId),
            PlayerFarmId = new EntityId(DeterministicGuid(worldSeed, 0xFA41UL)),
            RngState = DeterministicRngService.CreateInitialState(worldSeed)
        };
    }

    private static Guid DeterministicGuid(ulong seed, ulong salt)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes[..8], seed);
        BitConverter.TryWriteBytes(bytes[8..], salt);
        return new Guid(bytes);
    }
}

public static class EmptyDailySimulation
{
    public static void Advance(GameState state, int days)
    {
        if (days < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days));
        }

        var rng = new DeterministicRngService(state.WorldSeed, state.RngState);
        for (var day = 0; day < days; day++)
        {
            _ = rng.NextUInt64("weather");
            state.CurrentDateTime = state.CurrentDateTime.AddDays(1);
        }
    }
}

public static class CanonicalDiagnostics
{
    public static string Serialize(GameState state)
    {
        var builder = new StringBuilder();
        builder.Append("campaign=").Append(state.CampaignId).Append('\n');
        builder.Append("seed=").Append(state.WorldSeed.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("datetime=").Append(state.CurrentDateTime).Append('\n');
        builder.Append("difficulty=").Append(state.DifficultyProfileId).Append('\n');
        builder.Append("farm=").Append(state.PlayerFarmId).Append('\n');

        foreach (var stream in state.RngState.Streams.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append("rng.").Append(stream.Key).Append('=').Append(stream.Value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        return builder.ToString();
    }
}
