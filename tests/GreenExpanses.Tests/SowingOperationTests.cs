using GreenExpanses.Application.Commands;
using GreenExpanses.Domain;
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
}
