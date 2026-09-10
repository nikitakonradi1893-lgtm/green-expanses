using GreenExpanses.Application.Commands;
using GreenExpanses.Domain;
using GreenExpanses.Simulation;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class CropPlanningTests
{
    [Fact]
    public void OwnedField_CanReceiveCropPlan()
    {
        var state = InheritanceScenarioFactory.Create(20260910, new Money(1_000_000m));
        var fieldId = Assert.Single(state.Farm.OwnedFieldIds);
        var bus = FirstPlayableCommandBusFactory.Create();
        var commandId = EntityId.New();
        var correlationId = EntityId.New();

        var result = bus.Execute(state, new PlanCropCommand(
            commandId,
            correlationId,
            null,
            fieldId,
            new CatalogId("winter_wheat")));

        Assert.True(result.Succeeded);
        var plan = Assert.Single(state.Farm.CropPlans);
        Assert.Equal(fieldId, plan.FieldId);
        Assert.Equal(new CatalogId("winter_wheat"), plan.CropId);
        var domainEvent = Assert.Single(state.EventLog.Entries);
        Assert.Equal(new CatalogId("crop_plan_set"), domainEvent.EventType);
        Assert.Equal(commandId, domainEvent.CausationCommandId);
    }

    [Fact]
    public void RegionalField_CannotBePlanned()
    {
        var state = InheritanceScenarioFactory.Create(20260910, new Money(1_000_000m));
        var owned = Assert.Single(state.Farm.OwnedFieldIds);
        var other = state.World.Fields.First(field => field.Id != owned);
        var bus = FirstPlayableCommandBusFactory.Create();

        var result = bus.Execute(state, new PlanCropCommand(
            EntityId.New(),
            EntityId.New(),
            null,
            other.Id,
            new CatalogId("corn")));

        Assert.False(result.Succeeded);
        Assert.Equal("crop_plan.field_not_owned", result.Code);
        Assert.Empty(state.Farm.CropPlans);
    }

    [Fact]
    public void ReplanningField_ReplacesPreviousChoice()
    {
        var state = InheritanceScenarioFactory.Create(20260910, new Money(1_000_000m));
        var fieldId = Assert.Single(state.Farm.OwnedFieldIds);
        var bus = FirstPlayableCommandBusFactory.Create();

        bus.Execute(state, new PlanCropCommand(EntityId.New(), EntityId.New(), null, fieldId, new CatalogId("winter_wheat")));
        var result = bus.Execute(state, new PlanCropCommand(EntityId.New(), EntityId.New(), null, fieldId, new CatalogId("sunflower")));

        Assert.True(result.Succeeded);
        Assert.Equal(new CatalogId("sunflower"), Assert.Single(state.Farm.CropPlans).CropId);
        Assert.Equal(2, state.EventLog.Count);
    }
}
