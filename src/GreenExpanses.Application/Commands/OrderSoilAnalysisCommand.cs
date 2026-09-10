using GreenExpanses.Domain;

namespace GreenExpanses.Application.Commands;

public sealed record OrderSoilAnalysisCommand(
    EntityId CommandId,
    EntityId CorrelationId,
    EntityId? CausationId,
    EntityId FieldId,
    Money Cost,
    int DurationDays) : ICommand;

public sealed class OrderSoilAnalysisValidator : ICommandValidator<OrderSoilAnalysisCommand>
{
    public CommandCheckResult Validate(GameState state, OrderSoilAnalysisCommand command)
    {
        if (!state.World.FieldRegistry.Contains(command.FieldId))
            return CommandCheckResult.Reject("soil_analysis.field_missing", "soil_analysis.field_missing", [command.FieldId]);
        if (command.Cost.Value < 0m)
            return CommandCheckResult.Reject("soil_analysis.cost_invalid", "soil_analysis.cost_invalid");
        if (command.DurationDays <= 0)
            return CommandCheckResult.Reject("soil_analysis.duration_invalid", "soil_analysis.duration_invalid");
        if (state.Farm.SoilAnalysisOrders.Any(order => order.FieldId == command.FieldId && order.Status != new CatalogId("completed")))
            return CommandCheckResult.Reject("soil_analysis.already_pending", "soil_analysis.already_pending", [command.FieldId]);
        return CommandCheckResult.Allow();
    }
}

public sealed class OrderSoilAnalysisAuthorizer : ICommandAuthorizer<OrderSoilAnalysisCommand>
{
    public CommandCheckResult Authorize(GameState state, OrderSoilAnalysisCommand command)
    {
        if (!state.Farm.OwnedFieldIds.Contains(command.FieldId))
            return CommandCheckResult.Reject("soil_analysis.field_not_owned", "soil_analysis.field_not_owned", [command.FieldId]);
        if (state.Economy.Cash.Value < command.Cost.Value)
            return CommandCheckResult.Reject("soil_analysis.insufficient_cash", "soil_analysis.insufficient_cash", [command.FieldId]);
        return CommandCheckResult.Allow();
    }
}

public sealed class OrderSoilAnalysisExecutor : ICommandExecutor<OrderSoilAnalysisCommand>
{
    public CommandCheckResult Execute(GameState state, OrderSoilAnalysisCommand command)
    {
        var orderId = EntityId.New();
        var dueAt = state.CurrentDateTime.AddDays(command.DurationDays);
        state.Farm.SoilAnalysisOrders.Add(new SoilAnalysisOrder
        {
            OrderId = orderId,
            FieldId = command.FieldId,
            OrderedAt = state.CurrentDateTime,
            DueAt = dueAt,
            Cost = command.Cost,
            Status = new CatalogId("in_progress"),
            CausationCommandId = command.CommandId,
            CorrelationId = command.CorrelationId
        });

        if (command.Cost.Value > 0m)
        {
            state.Economy.PostTransaction(new TransactionRecord
            {
                TransactionId = EntityId.New(),
                GameDateTime = state.CurrentDateTime,
                Type = new CatalogId("soil_analysis_order"),
                CashDelta = new Money(-command.Cost.Value),
                RelatedEntityIds = [command.FieldId, orderId],
                DescriptionKey = new CatalogId("soil_analysis_ordered")
            });
        }

        state.EventLog.Append(new DomainEvent
        {
            EventId = EntityId.New(),
            GameDateTime = state.CurrentDateTime,
            EventType = new CatalogId("soil_analysis_ordered"),
            EntityIds = [command.FieldId, orderId],
            Payload = new DomainEventPayload()
                .Add("cost", command.Cost.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Add("duration_days", command.DurationDays.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Add("due_at", dueAt.ToString()),
            CausationCommandId = command.CommandId,
            CorrelationId = command.CorrelationId
        });

        return CommandCheckResult.Allow();
    }
}
