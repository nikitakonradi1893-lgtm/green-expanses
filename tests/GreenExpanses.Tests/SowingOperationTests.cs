using GreenExpanses.Application.Commands;
using GreenExpanses.Domain;
using GreenExpanses.Persistence;
using GreenExpanses.Simulation;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class SowingOperationTests
{
    [Fact]
    public void ScheduledSowing_DebitsCashAndCreatesOperation()
    {
        var state = InheritanceScenarioFactory.Create(20260910, new Money(1_000_000m));
        var fieldId = Assert.Single(state.Farm.OwnedFieldIds);
        var bus = FirstPlayableCommandBusFactory.Create();
        Assert.True(bus.Execute(state, new PlanCropCommand(EntityId.New(), EntityId.New(), null, fieldId, new CatalogId("winter_wheat"))).Succeeded);

        var result = bus.Execute(state, new ScheduleSowingOperationCommand(
            EntityId.New(), EntityId.New(), null, fieldId, 4.92m, new Money(40_000m)));

        Assert.True(result.Succeeded);
        var operation = Assert.Single(state.Farm.Operations);
        Assert.Equal(new CatalogId("sowing"), operation.OperationType);
        Assert.Equal(new CatalogId("planned"), operation.Status);
        Assert.Equal(new Money(960_000m), state.Economy.Cash);
        Assert.True(FinanceIntegrityCheck.Validate(state.Economy).IsValid);
        Assert.Equal(2, state.EventLog.Count);
    }

    [Fact]
    public void SowingWithoutCropPlan_IsRejected()
    {
        var state = InheritanceScenarioFactory.Create(20260910, new Money(1_000_000m));
        var fieldId = Assert.Single(state.Farm.OwnedFieldIds);
        var bus = FirstPlayableCommandBusFactory.Create();

        var result = bus.Execute(state, new ScheduleSowingOperationCommand(
            EntityId.New(), EntityId.New(), null, fieldId, 4.92m, new Money(40_000m)));

        Assert.False(result.Succeeded);
        Assert.Equal("operation.crop_plan_missing", result.Code);
        Assert.Empty(state.Farm.Operations);
    }

    [Fact]
    public void InsufficientCash_IsRejectedWithoutMutation()
    {
        var state = InheritanceScenarioFactory.Create(20260910, new Money(1_000m));
        var fieldId = Assert.Single(state.Farm.OwnedFieldIds);
        var bus = FirstPlayableCommandBusFactory.Create();
        Assert.True(bus.Execute(state, new PlanCropCommand(EntityId.New(), EntityId.New(), null, fieldId, new CatalogId("soybean"))).Succeeded);

        var result = bus.Execute(state, new ScheduleSowingOperationCommand(
            EntityId.New(), EntityId.New(), null, fieldId, 4.92m, new Money(40_000m)));

        Assert.False(result.Succeeded);
        Assert.Equal("operation.insufficient_cash", result.Code);
        Assert.Empty(state.Farm.Operations);
        Assert.Equal(new Money(1_000m), state.Economy.Cash);
    }

    [Fact]
    public void PlannedSowing_CanStartAndProgressByDay()
    {
        var state = CreateStateWithScheduledSowing(4.92m);
        var bus = FirstPlayableCommandBusFactory.Create();
        var operationId = Assert.Single(state.Farm.Operations).OperationId;

        var result = bus.Execute(state, new StartSowingOperationCommand(EntityId.New(), EntityId.New(), null, operationId));
        Assert.True(result.Succeeded);
        Assert.Equal(new CatalogId("in_progress"), Assert.Single(state.Farm.Operations).Status);

        EmptyDailySimulation.Advance(state, 1);

        var operation = Assert.Single(state.Farm.Operations);
        Assert.Equal(new CatalogId("in_progress"), operation.Status);
        Assert.True(operation.RemainingArea.Value < state.World.Fields.Single(field => field.Id == operation.FieldId).Area.Value);
        Assert.Contains(state.EventLog.Entries, domainEvent => domainEvent.EventType == new CatalogId("operation_progressed"));
    }

    [Fact]
    public void InProgressSowing_CompletesWhenRemainingAreaReachesZero()
    {
        var state = CreateStateWithScheduledSowing(20m);
        var bus = FirstPlayableCommandBusFactory.Create();
        var operationId = Assert.Single(state.Farm.Operations).OperationId;
        Assert.True(bus.Execute(state, new StartSowingOperationCommand(EntityId.New(), EntityId.New(), null, operationId)).Succeeded);

        EmptyDailySimulation.Advance(state, 1);

        var operation = Assert.Single(state.Farm.Operations);
        Assert.Equal(new CatalogId("completed"), operation.Status);
        Assert.Equal(new AreaHa(0m), operation.RemainingArea);
        Assert.Contains(state.EventLog.Entries, domainEvent => domainEvent.EventType == new CatalogId("operation_completed"));
    }

    [Fact]
    public void CompletedSowing_DoesNotProgressAgain()
    {
        var state = CreateStateWithScheduledSowing(20m);
        var bus = FirstPlayableCommandBusFactory.Create();
        var operationId = Assert.Single(state.Farm.Operations).OperationId;
        Assert.True(bus.Execute(state, new StartSowingOperationCommand(EntityId.New(), EntityId.New(), null, operationId)).Succeeded);
        EmptyDailySimulation.Advance(state, 1);
        var eventsAfterCompletion = state.EventLog.Count;

        EmptyDailySimulation.Advance(state, 3);

        Assert.Equal(new CatalogId("completed"), Assert.Single(state.Farm.Operations).Status);
        Assert.Equal(eventsAfterCompletion, state.EventLog.Count);
    }

    [Fact]
    public void SowingProgress_RoundTripsThroughSaveV1()
    {
        var state = CreateStateWithScheduledSowing(4.92m);
        var bus = FirstPlayableCommandBusFactory.Create();
        var operationId = Assert.Single(state.Farm.Operations).OperationId;
        Assert.True(bus.Execute(state, new StartSowingOperationCommand(EntityId.New(), EntityId.New(), null, operationId)).Succeeded);
        EmptyDailySimulation.Advance(state, 1);

        var json = SaveV1Serializer.Serialize(
            state,
            new ConfigVersion("test"),
            "test-build",
            DateTime.UnixEpoch,
            DateTime.UnixEpoch);
        var loaded = SaveV1Serializer.Deserialize(json).State;

        var operation = Assert.Single(loaded.Farm.Operations);
        Assert.Equal(new CatalogId("in_progress"), operation.Status);
        Assert.True(operation.RemainingArea.Value > 0m);
        Assert.True(operation.RemainingArea.Value < loaded.World.Fields.Single(field => field.Id == operation.FieldId).Area.Value);
    }

    private static GameState CreateStateWithScheduledSowing(decimal productivityHaPerHour)
    {
        var state = InheritanceScenarioFactory.Create(20260910, new Money(1_000_000m));
        var fieldId = Assert.Single(state.Farm.OwnedFieldIds);
        var bus = FirstPlayableCommandBusFactory.Create();
        Assert.True(bus.Execute(state, new PlanCropCommand(EntityId.New(), EntityId.New(), null, fieldId, new CatalogId("winter_wheat"))).Succeeded);
        Assert.True(bus.Execute(state, new ScheduleSowingOperationCommand(
            EntityId.New(), EntityId.New(), null, fieldId, productivityHaPerHour, new Money(40_000m))).Succeeded);
        return state;
    }
}
