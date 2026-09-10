using GreenExpanses.Application;
using GreenExpanses.Domain;
using GreenExpanses.Persistence;
using GreenExpanses.Simulation;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class FirstPlayableScenarioTests
{
    [Fact]
    public void InheritanceSelectsOneExistingFieldNearThirtyHectares()
    {
        var state = InheritanceScenarioFactory.Create(20260910UL, new Money(1_000_000m));

        Assert.Single(state.Farm.OwnedFieldIds);
        var field = Assert.Single(state.World.Fields, field => state.Farm.OwnedFieldIds.Contains(field.Id));
        Assert.True(field.DistanceKm <= 10m);
        Assert.InRange(field.Area.Value, 20m, 40m);
        Assert.Equal(1_000_000m, state.Economy.Cash.Value);
    }

    [Fact]
    public void ProjectionContainsOwnedFieldAndAdvancesThroughSimulation()
    {
        var state = InheritanceScenarioFactory.Create(77UL, new Money(1_000_000m));
        var before = FirstPlayableProjectionFactory.Build(state);

        EmptyDailySimulation.Advance(state, 7);
        var after = FirstPlayableProjectionFactory.Build(state);

        Assert.Equal(1, before.OwnedFieldCount);
        Assert.True(before.Fields[0].IsOwned);
        Assert.Equal(before.CurrentDateTime.AddDays(7), after.CurrentDateTime);
    }

    [Fact]
    public void SaveRoundTripKeepsInheritanceOwnershipAndCash()
    {
        var state = InheritanceScenarioFactory.Create(42UL, new Money(1_000_000m));
        EmptyDailySimulation.Advance(state, 3);
        var now = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);

        var json = SaveV1Serializer.Serialize(state, new ConfigVersion("1.0.0-dev"), "first-playable-test", now, now);
        var loaded = SaveV1Serializer.Deserialize(json).State;

        Assert.Equal(state.Farm.OwnedFieldIds, loaded.Farm.OwnedFieldIds);
        Assert.Equal(state.Economy.Cash, loaded.Economy.Cash);
        Assert.Equal(state.CurrentDateTime, loaded.CurrentDateTime);
    }
}
