using GreenExpanses.Domain;

namespace GreenExpanses.Application.Commands;

public sealed record ScheduleSowingOperationCommand(
    EntityId CommandId,
    EntityId CorrelationId,
    EntityId? CausationId,
    EntityId FieldId,
    decimal ProductivityHaPerHour,
    Money EstimatedCost) : ICommand;

public sealed record StartSowingOperationCommand(
    EntityId CommandId,
    EntityId CorrelationId,
    EntityId? CausationId,
    EntityId OperationId) : ICommand;

public sealed class ScheduleSowingOperationValidator : ICommandValidator<ScheduleSowingOperationCommand>
{
    public CommandCheckResult Validate(GameState state, ScheduleSowingOperationCommand command)
    {
        if (!state.World.FieldRegistry.Contains(command.FieldId))
            return CommandCheckResult.Reject("operation.field_missing", "operation.field_missing", [command.FieldId]);
        if (command.ProductivityHaPerHour <= 0m)
            return CommandCheckResult.Reject("operation.productivity_invalid", "operation.productivity_invalid");
        if (command.EstimatedCost.Value < 0m)
            return CommandCheckResult.Reject("operation.cost_invalid", "operation.cost_invalid");
        if (state.Farm.CropPlans.All(plan => plan.FieldId != command.FieldId))
            return CommandCheckResult.Reject("operation.crop_plan_missing", "operation.crop_plan_missing", [command.FieldId]);
        if (state.Farm.Operations.Any(operation => operation.FieldId == command.FieldId && operation.OperationType == new CatalogId("sowing")))
            return CommandCheckResult.Reject("operation.already_scheduled", "operation.already_scheduled", [command.FieldId]);
        return CommandCheckResult.Allow();
    }
}

public sealed class ScheduleSowingOperationAuthorizer : ICommandAuthorizer<ScheduleSowingOperationCommand>
{
    public CommandCheckResult Authorize(GameState state, ScheduleSowingOperationCommand command)
    {
        if (!state.Farm.OwnedFieldIds.Contains(command.FieldId))
            return CommandCheckResult.Reject("operation.field_not_owned", "operation.field_not_owned", [command.FieldId]);
        if (state.Economy.Cash.Value < command.EstimatedCost.Value)
            return CommandCheckResult.Reject("operation.insufficient_cash", "operation.insufficient_cash");
        return CommandCheckResult.Allow();
    }
}

public sealed class ScheduleSowingOperationExecutor : ICommandExecutor<ScheduleSowingOperationCommand>
{
    public CommandCheckResult Execute(GameState state, ScheduleSowingOperationCommand command)
    {
        var field = state.World.Fields.Single(item => item.Id == command.FieldId);
        var cropPlan = state.Farm.CropPlans.Single(plan => plan.FieldId == command.FieldId);
        var requiredHours = decimal.Round(field.Area.Value / command.ProductivityHaPerHour, 2, MidpointRounding.AwayFromZero);
        var operationId = EntityId.New();

        state.Farm.Operations.Add(new FieldOperationPlan
        {
            OperationId = operationId,
            FieldId = command.FieldId,
            CropId = cropPlan.CropId,
            OperationType = new CatalogId("sowing"),
            RemainingArea = field.Area,
            ProductivityHaPerHour = command.ProductivityHaPerHour,
            RequiredHours = requiredHours,
            EstimatedCost = command.EstimatedCost,
            PlannedAt = state.CurrentDateTime,
            Status = new CatalogId("planned")
        });

        if (command.EstimatedCost.Value > 0m)
        {
            state.Economy.PostTransaction(new TransactionRecord
            {
                TransactionId = EntityId.New(),
                GameDateTime = state.CurrentDateTime,
                Type = new CatalogId("operation_reservation"),
                CashDelta = new Money(-command.EstimatedCost.Value),
                RelatedEntityIds = [command.FieldId, operationId],
                DescriptionKey = new CatalogId("sowing_operation_reserved")
            });
        }

        state.EventLog.Append(new DomainEvent
        {
            EventId = EntityId.New(),
            GameDateTime = state.CurrentDateTime,
            EventType = new CatalogId("operation_scheduled"),
            EntityIds = [command.FieldId, operationId],
            Payload = new DomainEventPayload()
                .Add("operation_type", "sowing")
                .Add("crop_id", cropPlan.CropId.Value)
                .Add("required_hours", requiredHours.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Add("estimated_cost", command.EstimatedCost.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            CausationCommandId = command.CommandId,
            CorrelationId = command.CorrelationId
        });

        return CommandCheckResult.Allow();
    }
}

public sealed class StartSowingOperationValidator : ICommandValidator<StartSowingOperationCommand>
{
    public CommandCheckResult Validate(GameState state, StartSowingOperationCommand command)
    {
        var operation = state.Farm.Operations.FirstOrDefault(item => item.OperationId == command.OperationId);
        if (operation is null)
            return CommandCheckResult.Reject("operation.missing", "operation.missing", [command.OperationId]);
        if (operation.OperationType != new CatalogId("sowing"))
            return CommandCheckResult.Reject("operation.type_invalid", "operation.type_invalid", [command.OperationId]);
        if (operation.Status != new CatalogId("planned"))
            return CommandCheckResult.Reject("operation.not_planned", "operation.not_planned", [command.OperationId]);
        if (operation.RemainingArea.Value <= 0m)
            return CommandCheckResult.Reject("operation.no_remaining_area", "operation.no_remaining_area", [command.OperationId]);
        return CommandCheckResult.Allow();
    }
}

public sealed class StartSowingOperationAuthorizer : ICommandAuthorizer<StartSowingOperationCommand>
{
    public CommandCheckResult Authorize(GameState state, StartSowingOperationCommand command)
    {
        var operation = state.Farm.Operations.First(item => item.OperationId == command.OperationId);
        if (!state.Farm.OwnedFieldIds.Contains(operation.FieldId))
            return CommandCheckResult.Reject("operation.field_not_owned", "operation.field_not_owned", [operation.FieldId]);
        return CommandCheckResult.Allow();
    }
}

public sealed class StartSowingOperationExecutor : ICommandExecutor<StartSowingOperationCommand>
{
    public CommandCheckResult Execute(GameState state, StartSowingOperationCommand command)
    {
        var index = state.Farm.Operations.FindIndex(item => item.OperationId == command.OperationId);
        var operation = state.Farm.Operations[index];
        state.Farm.Operations[index] = operation with { Status = new CatalogId("in_progress") };

        state.EventLog.Append(new DomainEvent
        {
            EventId = EntityId.New(),
            GameDateTime = state.CurrentDateTime,
            EventType = new CatalogId("operation_started"),
            EntityIds = [operation.FieldId, operation.OperationId],
            Payload = new DomainEventPayload()
                .Add("operation_type", operation.OperationType.Value)
                .Add("remaining_area_ha", operation.RemainingArea.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            CausationCommandId = command.CommandId,
            CorrelationId = command.CorrelationId
        });

        return CommandCheckResult.Allow();
    }
}
