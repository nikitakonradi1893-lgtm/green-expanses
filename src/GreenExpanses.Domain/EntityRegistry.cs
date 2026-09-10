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
            if (!state.World.FieldRegistry.Contains(fieldId))
            {
                issues.Add(new ReferentialIntegrityIssue(
                    "farm.field_missing",
                    $"Player farm references field '{fieldId}' that is not registered in WorldState.",
                    state.Farm.PlayerFarmId,
                    fieldId));
            }
        }

        return new ReferentialIntegrityResult { Issues = issues };
    }
}
