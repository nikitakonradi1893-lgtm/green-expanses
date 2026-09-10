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
            Campaign = new CampaignState
            {
                Id = new EntityId(DeterministicGuid(worldSeed, 0xC0A1UL)),
                WorldSeed = worldSeed,
                DifficultyProfileId = new CatalogId(difficultyProfileId)
            },
            World = new WorldState(),
            Farm = new FarmState
            {
                PlayerFarmId = new EntityId(DeterministicGuid(worldSeed, 0xFA41UL))
            },
            Economy = new EconomyState(),
            Simulation = new SimulationState
            {
                CurrentDateTime = CampaignEpoch,
                RngState = DeterministicRngService.CreateInitialState(worldSeed),
                CompletedDays = 0
            }
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
        ArgumentNullException.ThrowIfNull(state);
        if (days < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days));
        }

        var rng = new DeterministicRngService(state.Campaign.WorldSeed, state.Simulation.RngState);
        for (var day = 0; day < days; day++)
        {
            _ = rng.NextUInt64(RngStreams.Weather);
            state.Simulation.CurrentDateTime = state.Simulation.CurrentDateTime.AddDays(1);
            state.Simulation.CompletedDays++;
        }

        state.Simulation.RngState = rng.Snapshot();
    }
}

public static class CanonicalDiagnostics
{
    public static string Serialize(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var builder = new StringBuilder();
        builder.Append("campaign=").Append(state.Campaign.Id).Append('\n');
        builder.Append("seed=").Append(state.Campaign.WorldSeed.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("datetime=").Append(state.Simulation.CurrentDateTime).Append('\n');
        builder.Append("completed_days=").Append(state.Simulation.CompletedDays.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("difficulty=").Append(state.Campaign.DifficultyProfileId).Append('\n');
        builder.Append("farm=").Append(state.Farm.PlayerFarmId).Append('\n');
        builder.Append("cash=").Append(state.Economy.Cash).Append('\n');
        builder.Append("debt=").Append(state.Economy.Debt).Append('\n');

        foreach (var stream in state.Simulation.RngState.Streams.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append("rng.").Append(stream.Key).Append('=').Append(stream.Value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        return builder.ToString();
    }
}
