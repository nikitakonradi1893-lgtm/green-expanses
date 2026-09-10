using GreenExpanses.Domain;

namespace GreenExpanses.Application.Commands;

public sealed record PlanCropCommand(
    EntityId CommandId,
    EntityId CorrelationId,
    EntityId? CausationId,
    EntityId FieldId,
    CatalogId CropId) : ICommand;

public static class FirstPlayableCrops
{
    public static readonly IReadOnlyList<CatalogId> All =
    [
        new("winter_wheat"),
        new("grain_corn"),
        new("sunflower"),
        new("soybean")
    ];

    public static CatalogId Normalize(CatalogId cropId) => cropId.Value switch
    {
        "corn" => new CatalogId("grain_corn"),
        "soy" => new CatalogId("soybean"),
        _ => cropId
    };

    public static bool Contains(CatalogId cropId) => All.Contains(Normalize(cropId));
}

public sealed class PlanCropValidator : ICommandValidator<PlanCropCommand>
{
    public CommandCheckResult Validate(GameState state, PlanCropCommand command)
    {
        if (!state.World.FieldRegistry.Contains(command.FieldId))
            return CommandCheckResult.Reject("crop_plan.field_missing", "crop_plan.field_missing", [command.FieldId]);
        if (!FirstPlayableCrops.Contains(command.CropId))
            return CommandCheckResult.Reject("crop_plan.crop_unsupported", "crop_plan.crop_unsupported");
        return CommandCheckResult.Allow();
    }
}

public sealed class PlanCropAuthorizer : ICommandAuthorizer<PlanCropCommand>
{
    public CommandCheckResult Authorize(GameState state, PlanCropCommand command)
    {
        if (!state.Farm.OwnedFieldIds.Contains(command.FieldId))
            return CommandCheckResult.Reject("crop_plan.field_not_owned", "crop_plan.field_not_owned", [command.FieldId]);
        return CommandCheckResult.Allow();
    }
}

public sealed class PlanCropExecutor : ICommandExecutor<PlanCropCommand>
{
    public CommandCheckResult Execute(GameState state, PlanCropCommand command)
    {
        var canonicalCropId = FirstPlayableCrops.Normalize(command.CropId);
        var existing = state.Farm.CropPlans.FindIndex(plan => plan.FieldId == command.FieldId);
        var plan = new FieldCropPlan { FieldId = command.FieldId, CropId = canonicalCropId, PlannedAt = state.CurrentDateTime };
        if (existing >= 0) state.Farm.CropPlans[existing] = plan;
        else state.Farm.CropPlans.Add(plan);

        state.EventLog.Append(new DomainEvent
        {
            EventId = EntityId.New(), GameDateTime = state.CurrentDateTime,
            EventType = new CatalogId("crop_plan_set"), EntityIds = [command.FieldId],
            Payload = new DomainEventPayload().Add("crop_id", canonicalCropId.Value),
            CausationCommandId = command.CommandId, CorrelationId = command.CorrelationId
        });
        return CommandCheckResult.Allow();
    }
}

public static class FirstPlayableCommandBusFactory
{
    public static CommandBus Create()
    {
        var bus = new CommandBus();
        bus.Register(new PlanCropExecutor(), [new PlanCropValidator()], [new PlanCropAuthorizer()]);
        bus.Register(new ScheduleSowingOperationExecutor(), [new ScheduleSowingOperationValidator()], [new ScheduleSowingOperationAuthorizer()]);
        return bus;
    }
}
