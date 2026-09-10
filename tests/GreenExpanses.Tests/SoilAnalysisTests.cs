using GreenExpanses.Application.Commands;
using GreenExpanses.Domain;
using GreenExpanses.Persistence;
using GreenExpanses.Simulation;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class SoilAnalysisTests
{
    [Fact]
    public void OwnedField_CanOrderSoilAnalysis_AndPaysOnce()
    {
        var state = InheritanceScenarioFactory.Create(20260910, new Money(1_000_000m));
        var fieldId = Assert.Single(state.Farm.OwnedFieldIds);
        var bus = FirstPlayableCommandBusFactory.Create();
        var commandId = EntityId.New();
        var correlationId = EntityId.New();

        var result = bus.Execute(state, new OrderSoilAnalysisCommand(commandId, correlationId, null, fieldId, new Money(18_500m), 3));

        Assert.True(result.Succeeded);
        Assert.Equal(new Money(981_500m), state.Economy.Cash);
        var order = Assert.Single(state.Farm.SoilAnalysisOrders);
        Assert.Equal(fieldId, order.FieldId);
        Assert.Equal(new CatalogId("in_progress"), order.Status);
        Assert.Equal(state.CurrentDateTime.AddDays(3), order.DueAt);
        Assert.Equal(commandId, order.CausationCommandId);
        Assert.Single(state.Economy.Ledger.Entries);
        Assert.Single(state.EventLog.Entries);
    }

    [Fact]
    public void SoilAnalysis_CompletesAfterDueDate()
    {
        var state = InheritanceScenarioFactory.Create(20260910, new Money(1_000_000m));
        var fieldId = Assert.Single(state.Farm.OwnedFieldIds);
        var bus = FirstPlayableCommandBusFactory.Create();

        var result = bus.Execute(state, new OrderSoilAnalysisCommand(EntityId.New(), EntityId.New(), null, fieldId, new Money(18_500m), 3));
        Assert.True(result.Succeeded);

        EmptyDailySimulation.Advance(state, 2);
        Assert.Equal(new CatalogId("in_progress"), Assert.Single(state.Farm.SoilAnalysisOrders).Status);

        EmptyDailySimulation.Advance(state, 1);
        Assert.Equal(new CatalogId("completed"), Assert.Single(state.Farm.SoilAnalysisOrders).Status);
        Assert.Contains(state.EventLog.Entries, domainEvent => domainEvent.EventType == new CatalogId("soil_analysis_completed"));
        Assert.Equal(new Money(981_500m), state.Economy.Cash);
        Assert.Single(state.Economy.Ledger.Entries);
    }

    [Fact]
    public void RegionalField_CannotOrderSoilAnalysis()
    {
        var state = InheritanceScenarioFactory.Create(20260910, new Money(1_000_000m));
        var owned = Assert.Single(state.Farm.OwnedFieldIds);
        var regional = state.World.Fields.First(field => field.Id != owned);
        var bus = FirstPlayableCommandBusFactory.Create();

        var result = bus.Execute(state, new OrderSoilAnalysisCommand(EntityId.New(), EntityId.New(), null, regional.Id, new Money(18_500m), 3));

        Assert.False(result.Succeeded);
        Assert.Equal("soil_analysis.field_not_owned", result.Code);
        Assert.Empty(state.Farm.SoilAnalysisOrders);
        Assert.Empty(state.Economy.Ledger.Entries);
    }

    [Fact]
    public void DuplicatePendingOrder_IsRejected()
    {
        var state = InheritanceScenarioFactory.Create(20260910, new Money(1_000_000m));
        var fieldId = Assert.Single(state.Farm.OwnedFieldIds);
        var bus = FirstPlayableCommandBusFactory.Create();

        Assert.True(bus.Execute(state, new OrderSoilAnalysisCommand(EntityId.New(), EntityId.New(), null, fieldId, new Money(18_500m), 3)).Succeeded);
        var duplicate = bus.Execute(state, new OrderSoilAnalysisCommand(EntityId.New(), EntityId.New(), null, fieldId, new Money(18_500m), 3));

        Assert.False(duplicate.Succeeded);
        Assert.Equal("soil_analysis.already_pending", duplicate.Code);
        Assert.Single(state.Farm.SoilAnalysisOrders);
        Assert.Single(state.Economy.Ledger.Entries);
    }

    [Fact]
    public void SoilAnalysisOrder_RoundTripsThroughSave()
    {
        var state = InheritanceScenarioFactory.Create(20260910, new Money(1_000_000m));
        var fieldId = Assert.Single(state.Farm.OwnedFieldIds);
        var bus = FirstPlayableCommandBusFactory.Create();

        Assert.True(bus.Execute(state, new OrderSoilAnalysisCommand(EntityId.New(), EntityId.New(), null, fieldId, new Money(18_500m), 3)).Succeeded);
        EmptyDailySimulation.Advance(state, 1);

        var json = SaveV1Serializer.Serialize(
            state,
            new ConfigVersion("1.0.0-dev"),
            "soil-analysis-test",
            DateTime.UnixEpoch,
            DateTime.UnixEpoch);
        var loaded = SaveV1Serializer.Deserialize(json).State;

        var order = Assert.Single(loaded.Farm.SoilAnalysisOrders);
        Assert.Equal(fieldId, order.FieldId);
        Assert.Equal(new CatalogId("in_progress"), order.Status);
        Assert.Equal(new Money(981_500m), loaded.Economy.Cash);
        Assert.Single(loaded.Economy.Ledger.Entries);

        EmptyDailySimulation.Advance(loaded, 2);
        Assert.Equal(new CatalogId("completed"), Assert.Single(loaded.Farm.SoilAnalysisOrders).Status);
    }
}
