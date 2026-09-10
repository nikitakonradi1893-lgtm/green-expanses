namespace GreenExpanses.Domain;

public sealed class EntityRegistry
{
    private readonly HashSet<EntityId> _ids = [];

    public int Count => _ids.Count;
    public IReadOnlyCollection<EntityId> Ids => _ids;

    public bool Contains(EntityId id) => _ids.Contains(id);

    public void Add(EntityId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Entity ID cannot be empty.", nameof(id));
        }

        if (!_ids.Add(id))
        {
            throw new InvalidOperationException($"Entity ID '{id}' is already registered.");
        }
    }

    public bool Remove(EntityId id) => _ids.Remove(id);
}

public sealed record ReferentialIntegrityIssue(
    string Code,
    string Message,
    EntityId ReferencingEntityId,
    EntityId MissingEntityId);

public sealed class ReferentialIntegrityResult
{
    public required IReadOnlyList<ReferentialIntegrityIssue> Issues { get; init; }
    public bool IsValid => Issues.Count == 0;
}

public static class ReferentialIntegrityCheck
{
    public static ReferentialIntegrityResult Validate(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var issues = new List<ReferentialIntegrityIssue>();
        foreach (var fieldId in state.Farm.OwnedFieldIds)
        {
            AddMissingFieldIssue(issues, state, state.Farm.PlayerFarmId, fieldId, "farm.field_missing");
        }

        foreach (var cropPlan in state.Farm.CropPlans)
        {
            AddMissingFieldIssue(issues, state, state.Farm.PlayerFarmId, cropPlan.FieldId, "crop_plan.field_missing");
        }

        foreach (var operation in state.Farm.Operations)
        {
            AddMissingFieldIssue(issues, state, operation.OperationId, operation.FieldId, "operation.field_missing");
        }

        foreach (var order in state.Farm.SoilAnalysisOrders)
        {
            AddMissingFieldIssue(issues, state, order.OrderId, order.FieldId, "soil_analysis.field_missing");
        }

        return new ReferentialIntegrityResult { Issues = issues };
    }

    private static void AddMissingFieldIssue(
        List<ReferentialIntegrityIssue> issues,
        GameState state,
        EntityId referencingEntityId,
        EntityId fieldId,
        string code)
    {
        if (state.World.FieldRegistry.Contains(fieldId))
        {
            return;
        }

        issues.Add(new ReferentialIntegrityIssue(
            code,
            $"Entity '{referencingEntityId}' references field '{fieldId}' that is not registered in WorldState.",
            referencingEntityId,
            fieldId));
    }
}
