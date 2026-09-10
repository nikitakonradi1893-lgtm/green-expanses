using System.Text.Json;
using GreenExpanses.Domain;
using GreenExpanses.Persistence;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class SaveV1Tests
{
    [Fact]
    public void RoundTripPreservesDeterministicState()
    {
        var state = CreateState();
        var json = SaveV1Serializer.Serialize(
            state,
            new ConfigVersion("1.0"),
            "test-build",
            new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 10, 10, 5, 0, DateTimeKind.Utc));

        var loaded = SaveV1Serializer.Deserialize(json);

        Assert.Equal(state.CampaignId, loaded.State.CampaignId);
        Assert.Equal(state.WorldSeed, loaded.State.WorldSeed);
        Assert.Equal(state.CurrentDateTime, loaded.State.CurrentDateTime);
        Assert.Equal(state.Simulation.CompletedDays, loaded.State.Simulation.CompletedDays);
        Assert.Equal(state.RngState.Streams, loaded.State.RngState.Streams);
        Assert.Equal(state.World.FieldIds.OrderBy(x => x.Value), loaded.State.World.FieldIds.OrderBy(x => x.Value));
        Assert.Equal(state.Farm.OwnedFieldIds, loaded.State.Farm.OwnedFieldIds);
        Assert.Equal(state.Economy.OpeningCash, loaded.State.Economy.OpeningCash);
        Assert.Equal(state.Economy.Cash, loaded.State.Economy.Cash);
        Assert.Equal(state.Economy.Debt, loaded.State.Economy.Debt);
        Assert.Equal(state.Economy.Ledger.Count, loaded.State.Economy.Ledger.Count);
        Assert.Equal(state.EventLog.Count, loaded.State.EventLog.Count);
        Assert.True(FinanceIntegrityCheck.Validate(loaded.State.Economy).IsValid);
        Assert.True(ReferentialIntegrityCheck.Validate(loaded.State).IsValid);
        Assert.Equal(new ConfigVersion("1.0"), loaded.Header.ConfigVersion);
    }

    [Fact]
    public void TamperedStateFailsChecksumValidation()
    {
        var state = CreateState();
        var json = SaveV1Serializer.Serialize(
            state,
            new ConfigVersion("1.0"),
            "test-build",
            DateTime.UtcNow,
            DateTime.UtcNow);

        using var document = JsonDocument.Parse(json);
        var tampered = json.Replace("\"worldSeed\":123456789", "\"worldSeed\":123456788", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => SaveV1Serializer.Deserialize(tampered));
    }

    [Fact]
    public void AutosaveUsesOneAtomicSlotAndPreservesCreatedTimestamp()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"green-expanses-{Guid.NewGuid():N}");
        try
        {
            var times = new Queue<DateTime>(new[]
            {
                new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 9, 10, 11, 0, 0, DateTimeKind.Utc)
            });
            var store = new AutosaveStore(directory, () => times.Dequeue());
            var state = CreateState();

            var first = store.Save(state, new ConfigVersion("1.0"), "build-a");
            state.CurrentDateTime = state.CurrentDateTime.AddDays(1);
            var second = store.Save(state, new ConfigVersion("1.0"), "build-b");
            var loaded = store.Load();

            Assert.True(File.Exists(store.SlotPath));
            Assert.False(File.Exists(store.SlotPath + ".tmp"));
            Assert.Equal(first.CreatedAtRealTime, second.CreatedAtRealTime);
            Assert.Equal(new DateTime(2026, 9, 10, 11, 0, 0, DateTimeKind.Utc), second.LastSavedAtRealTime);
            Assert.Equal(state.CurrentDateTime, loaded.State.CurrentDateTime);
            Assert.Equal("build-b", loaded.Header.BuildVersion);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static GameState CreateState()
    {
        var campaignId = new EntityId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var farmId = new EntityId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var fieldId = new EntityId(Guid.Parse("30000000-0000-0000-0000-000000000001"));
        var commandId = new EntityId(Guid.Parse("40000000-0000-0000-0000-000000000001"));
        var correlationId = new EntityId(Guid.Parse("50000000-0000-0000-0000-000000000001"));
        var world = new WorldState();
        world.FieldRegistry.Add(fieldId);

        var economy = new EconomyState(new Money(100_000m))
        {
            Debt = new Money(25_000m)
        };
        economy.PostTransaction(new TransactionRecord
        {
            TransactionId = new EntityId(Guid.Parse("60000000-0000-0000-0000-000000000001")),
            GameDateTime = new GameDateTime(new DateTime(2026, 3, 1, 8, 0, 0)),
            Type = new CatalogId("seed_purchase"),
            CashDelta = new Money(-12_500m),
            RelatedEntityIds = [fieldId],
            DescriptionKey = new CatalogId("seed_purchase")
        });

        var events = new EventLog();
        events.Append(new DomainEvent
        {
            EventId = new EntityId(Guid.Parse("70000000-0000-0000-0000-000000000001")),
            GameDateTime = new GameDateTime(new DateTime(2026, 3, 1, 8, 0, 0)),
            EventType = new CatalogId("resource_consumed"),
            EntityIds = [fieldId],
            Payload = new DomainEventPayload().Add("quantity", "12500"),
            CausationCommandId = commandId,
            CorrelationId = correlationId
        });

        return new GameState
        {
            Campaign = new CampaignState
            {
                Id = campaignId,
                WorldSeed = 123456789UL,
                DifficultyProfileId = new CatalogId("heritage")
            },
            World = world,
            Farm = new FarmState
            {
                PlayerFarmId = farmId,
                OwnedFieldIds = [fieldId]
            },
            Economy = economy,
            Simulation = new SimulationState
            {
                CurrentDateTime = new GameDateTime(new DateTime(2026, 3, 1, 8, 0, 0)),
                CompletedDays = 17,
                RngState = new RngState
                {
                    Streams = new Dictionary<string, ulong>(StringComparer.Ordinal)
                    {
                        ["weather"] = 111,
                        ["market"] = 222,
                        ["failures"] = 333
                    }
                }
            },
            EventLog = events
        };
    }
}
