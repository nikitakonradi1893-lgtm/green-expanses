namespace GreenExpanses.Domain;

public sealed class DomainEventPayload
{
    private readonly SortedDictionary<string, string> _values = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Values => _values;

    public DomainEventPayload Add(string key, string value)
    {
        _ = new CatalogId(key);
        _values.Add(key, Validation.NotBlank(value, nameof(value)));
        return this;
    }
}

public sealed record DomainEvent
{
    public required EntityId EventId { get; init; }
    public required GameDateTime GameDateTime { get; init; }
    public required CatalogId EventType { get; init; }
    public required IReadOnlyList<EntityId> EntityIds { get; init; }
    public required DomainEventPayload Payload { get; init; }
    public required EntityId CausationCommandId { get; init; }
    public required EntityId CorrelationId { get; init; }
}

public sealed class EventLog
{
    private readonly List<DomainEvent> _entries = [];
    private readonly HashSet<EntityId> _eventIds = [];

    public IReadOnlyList<DomainEvent> Entries => _entries;
    public int Count => _entries.Count;

    public void Append(DomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.EventId.Value == Guid.Empty)
        {
            throw new ArgumentException("Event ID cannot be empty.", nameof(domainEvent));
        }

        if (domainEvent.CausationCommandId.Value == Guid.Empty)
        {
            throw new ArgumentException("Causation command ID cannot be empty.", nameof(domainEvent));
        }

        if (domainEvent.CorrelationId.Value == Guid.Empty)
        {
            throw new ArgumentException("Correlation ID cannot be empty.", nameof(domainEvent));
        }

        if (!_eventIds.Add(domainEvent.EventId))
        {
            throw new InvalidOperationException($"Domain event '{domainEvent.EventId}' is already present in EventLog.");
        }

        _entries.Add(domainEvent);
    }
}
