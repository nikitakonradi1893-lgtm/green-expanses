using GreenExpanses.Infrastructure;
using GreenExpanses.Simulation;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class GameStateRootTests
{
    [Fact]
    public void Factory_CreatesAllRootSections()
    {
        var state = GameStateFactory.Create(42UL);

        Assert.NotNull(state.Campaign);
        Assert.NotNull(state.World);
        Assert.NotNull(state.Farm);
        Assert.NotNull(state.Economy);
        Assert.NotNull(state.Simulation);
        Assert.Equal(42UL, state.Campaign.WorldSeed);
        Assert.Equal(state.Campaign.Id, state.CampaignId);
        Assert.Equal(state.Farm.PlayerFarmId, state.PlayerFarmId);
    }

    [Fact]
    public void NewCampaign_StartsWithEmptyWorldAndEconomy()
    {
        var state = GameStateFactory.Create(42UL);

        Assert.Empty(state.World.FieldIds);
        Assert.Empty(state.Farm.OwnedFieldIds);
        Assert.Equal(0m, state.Economy.Cash.Value);
        Assert.Equal(0m, state.Economy.Debt.Value);
        Assert.Equal(0, state.Simulation.CompletedDays);
    }

    [Fact]
    public void Advance_UpdatesClockCounterAndPersistedRngState()
    {
        var state = GameStateFactory.Create(42UL);
        var initialWeatherState = state.Simulation.RngState.Streams[RngStreams.Weather];

        EmptyDailySimulation.Advance(state, 7);

        Assert.Equal("2026-01-08T00:00:00", state.Simulation.CurrentDateTime.ToString());
        Assert.Equal(7, state.Simulation.CompletedDays);
        Assert.NotEqual(initialWeatherState, state.Simulation.RngState.Streams[RngStreams.Weather]);
    }

    [Fact]
    public void ChunkedAdvance_EqualsSingleAdvance()
    {
        var single = GameStateFactory.Create(2026UL);
        var chunked = GameStateFactory.Create(2026UL);

        EmptyDailySimulation.Advance(single, 30);
        EmptyDailySimulation.Advance(chunked, 10);
        EmptyDailySimulation.Advance(chunked, 20);

        Assert.Equal(CanonicalDiagnostics.Serialize(single), CanonicalDiagnostics.Serialize(chunked));
    }
}
