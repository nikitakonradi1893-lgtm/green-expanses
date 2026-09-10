using GreenExpanses.Domain;
using GreenExpanses.Simulation;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class EventLogTests
{
    [Fact]
    public void NewGame_HasEmptyEventLog()
    {
        var state = GameStateFactory.Create(42UL);

        Assert.NotNull(state.EventLog);
        Assert.Empty(state.EventLog.Entries);
    }

    [Fact]
    public void Append_PreservesInsertionOrderAndMetadata()
    {
        var log = new EventLog();
        var correlationId = Id();
        var commandId = Id();
        var first = Event("operation_started", commandId, correlationId);
        var second = Event("operation_completed", commandId, correlationId);

        log.Append(first);
        log.Append(second);

        Assert.Equal(2, log.Count);
        Assert.Same(first, log.Entries[0]);
        Assert.Same(second, log.Entries[1]);
        Assert.Equal(commandId, log.Entries[0].CausationCommandId);
        Assert.Equal(correlationId, log.Entries[1].CorrelationId);
    }

    [Fact]
    public void Append_RejectsDuplicateEventId()
    {
        var log = new EventLog();
        var eventId = Id();
        var first = Event("operation_started", Id(), Id()) with { EventId = eventId };
        var duplicate = Event("operation_completed", Id(), Id()) with { EventId = eventId };

        log.Append(first);

        Assert.Throws<InvalidOperationException>(() => log.Append(duplicate));
    }

    [Fact]
    public void Payload_UsesStableOrdinalKeyOrderAndRejectsBadKeys()
    {
        var payload = new DomainEventPayload()
            .Add("z_value", "3")
            .Add("a_value", "1")
            .Add("m_value", "2");

        Assert.Equal(new[] { "a_value", "m_value", "z_value" }, payload.Values.Keys);
        Assert.Throws<ArgumentException>(() => new DomainEventPayload().Add("Bad-Key", "value"));
        Assert.Throws<ArgumentException>(() => new DomainEventPayload().Add("good_key", " "));
    }

    private static DomainEvent Event(string type, EntityId causation, EntityId correlation) => new()
    {
        EventId = Id(),
        GameDateTime = new GameDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)),
        EventType = new CatalogId(type),
        EntityIds = [Id()],
        Payload = new DomainEventPayload().Add("reason_code", "test"),
        CausationCommandId = causation,
        CorrelationId = correlation
    };

    private static EntityId Id() => new(Guid.NewGuid());
}
