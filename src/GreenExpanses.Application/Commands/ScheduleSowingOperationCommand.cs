using GreenExpanses.Domain;

namespace GreenExpanses.Application.Commands;

public sealed record ScheduleSowingOperationCommand(
    EntityId CommandId,
    EntityId CorrelationId,
    EntityId? CausationId,
    EntityId FieldId,
    decimal ProductivityHaPerHour,
    Money EstimatedCost) : ICommand;

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
