using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GreenExpanses.Domain;

namespace GreenExpanses.Persistence;

public sealed record SaveHeader(
    int SaveSchemaVersion,
    ConfigVersion ConfigVersion,
    string BuildVersion,
    EntityId CampaignId,
    ulong WorldSeed,
    GameDateTime CurrentDateTime,
    DateTime CreatedAtRealTime,
    DateTime LastSavedAtRealTime,
    string Checksum);

public sealed record SaveLoadResult(GameState State, SaveHeader Header);

public static class SaveV1Serializer
{
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(
        GameState state,
        ConfigVersion configVersion,
        string buildVersion,
        DateTime createdAtRealTime,
        DateTime lastSavedAtRealTime)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(buildVersion))
        {
            throw new ArgumentException("Build version cannot be blank.", nameof(buildVersion));
        }

        var dto = GameStateDto.FromDomain(state);
        var checksum = ComputeChecksum(dto);
        var envelope = new SaveEnvelope
        {
            Header = new SaveHeaderDto
            {
                SaveSchemaVersion = SchemaVersion,
                ConfigVersion = configVersion.Value,
                BuildVersion = buildVersion,
                CampaignId = state.CampaignId.Value,
                WorldSeed = state.WorldSeed,
                CurrentDateTime = state.CurrentDateTime.Value,
                CreatedAtRealTime = DateTime.SpecifyKind(createdAtRealTime, DateTimeKind.Utc),
                LastSavedAtRealTime = DateTime.SpecifyKind(lastSavedAtRealTime, DateTimeKind.Utc),
                Checksum = checksum
            },
            State = dto
        };

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    public static SaveLoadResult Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("Save data is empty.");
        }

        var envelope = JsonSerializer.Deserialize<SaveEnvelope>(json, JsonOptions)
            ?? throw new InvalidDataException("Save data could not be deserialized.");

        if (envelope.Header.SaveSchemaVersion != SchemaVersion)
        {
            throw new InvalidDataException($"Unsupported save schema version '{envelope.Header.SaveSchemaVersion}'.");
        }

        var storedChecksum = string.IsNullOrWhiteSpace(envelope.Header.Checksum)
            ? throw new InvalidDataException("Save checksum is missing.")
            : envelope.Header.Checksum;
        var actualChecksum = ComputeChecksum(envelope.State);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(actualChecksum),
                Encoding.ASCII.GetBytes(storedChecksum)))
        {
            throw new InvalidDataException("Save checksum mismatch.");
        }

        var state = envelope.State.ToDomain();
        var referential = ReferentialIntegrityCheck.Validate(state);
        if (!referential.IsValid)
        {
            throw new InvalidDataException($"Save referential integrity failed: {referential.Issues[0].Code}.");
        }

        var finance = FinanceIntegrityCheck.Validate(state.Economy);
        if (!finance.IsValid)
        {
            throw new InvalidDataException($"Save finance integrity failed: {finance.Code}.");
        }

        var header = new SaveHeader(
            envelope.Header.SaveSchemaVersion,
            new ConfigVersion(envelope.Header.ConfigVersion),
            envelope.Header.BuildVersion,
            new EntityId(envelope.Header.CampaignId),
            envelope.Header.WorldSeed,
            new GameDateTime(envelope.Header.CurrentDateTime),
            envelope.Header.CreatedAtRealTime,
            envelope.Header.LastSavedAtRealTime,
            storedChecksum);

        if (header.CampaignId != state.CampaignId ||
            header.WorldSeed != state.WorldSeed ||
            header.CurrentDateTime != state.CurrentDateTime)
        {
            throw new InvalidDataException("Save header does not match the serialized GameState.");
        }

        return new SaveLoadResult(state, header);
    }

    private static string ComputeChecksum(GameStateDto state)
    {
        var canonicalState = JsonSerializer.Serialize(state, JsonOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalState));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class SaveEnvelope
    {
        public required SaveHeaderDto Header { get; init; }
        public required GameStateDto State { get; init; }
    }

    private sealed class SaveHeaderDto
    {
        public int SaveSchemaVersion { get; init; }
        public required string ConfigVersion { get; init; }
        public required string BuildVersion { get; init; }
        public Guid CampaignId { get; init; }
        public ulong WorldSeed { get; init; }
        public DateTime CurrentDateTime { get; init; }
        public DateTime CreatedAtRealTime { get; init; }
        public DateTime LastSavedAtRealTime { get; init; }
        public required string Checksum { get; init; }
    }

    private sealed class GameStateDto
    {
        public Guid CampaignId { get; init; }
        public ulong WorldSeed { get; init; }
        public required string DifficultyProfileId { get; init; }
        public Guid PlayerFarmId { get; init; }
        public DateTime CurrentDateTime { get; init; }
        public long CompletedDays { get; init; }
        public required List<Guid> FieldIds { get; init; }
        public required List<FieldDto> Fields { get; init; }
        public required List<Guid> OwnedFieldIds { get; init; }
        public decimal OpeningCash { get; init; }
        public decimal Debt { get; init; }
        public required List<TransactionDto> Transactions { get; init; }
        public required SortedDictionary<string, ulong> RngStreams { get; init; }
        public required List<DomainEventDto> Events { get; init; }

        public static GameStateDto FromDomain(GameState state)
        {
            return new GameStateDto
            {
                CampaignId = state.CampaignId.Value,
                WorldSeed = state.WorldSeed,
                DifficultyProfileId = state.DifficultyProfileId.Value,
                PlayerFarmId = state.PlayerFarmId.Value,
                CurrentDateTime = state.CurrentDateTime.Value,
                CompletedDays = state.Simulation.CompletedDays,
                FieldIds = state.World.FieldIds.Select(id => id.Value).OrderBy(id => id).ToList(),
                Fields = state.World.Fields
                    .OrderBy(static field => field.Id.Value)
                    .Select(FieldDto.FromDomain)
                    .ToList(),
                OwnedFieldIds = state.Farm.OwnedFieldIds.Select(id => id.Value).ToList(),
                OpeningCash = state.Economy.OpeningCash.Value,
                Debt = state.Economy.Debt.Value,
                Transactions = state.Economy.Ledger.Entries.Select(TransactionDto.FromDomain).ToList(),
                RngStreams = new SortedDictionary<string, ulong>(state.RngState.Streams, StringComparer.Ordinal),
                Events = state.EventLog.Entries.Select(DomainEventDto.FromDomain).ToList()
            };
        }

        public GameState ToDomain()
        {
            var world = new WorldState();
            foreach (var field in Fields)
            {
                world.AddField(field.ToDomain());
            }

            foreach (var fieldId in FieldIds)
            {
                var id = new EntityId(fieldId);
                if (!world.FieldRegistry.Contains(id))
                {
                    world.FieldRegistry.Add(id);
                }
            }

            var economy = new EconomyState(new Money(OpeningCash))
            {
                Debt = new Money(Debt)
            };
            foreach (var transaction in Transactions)
            {
                economy.PostTransaction(transaction.ToDomain());
            }

            var eventLog = new EventLog();
            foreach (var domainEvent in Events)
            {
                eventLog.Append(domainEvent.ToDomain());
            }

            return new GameState
            {
                Campaign = new CampaignState
                {
                    Id = new EntityId(CampaignId),
                    WorldSeed = WorldSeed,
                    DifficultyProfileId = new CatalogId(DifficultyProfileId)
                },
                World = world,
                Farm = new FarmState
                {
                    PlayerFarmId = new EntityId(PlayerFarmId),
                    OwnedFieldIds = OwnedFieldIds.Select(id => new EntityId(id)).ToList()
                },
                Economy = economy,
                Simulation = new SimulationState
                {
                    CurrentDateTime = new GameDateTime(CurrentDateTime),
                    CompletedDays = CompletedDays,
                    RngState = new RngState
                    {
                        Streams = new Dictionary<string, ulong>(RngStreams, StringComparer.Ordinal)
                    }
                },
                EventLog = eventLog
            };
        }
    }

    private sealed class FieldDto
    {
        public Guid Id { get; init; }
        public decimal AreaHa { get; init; }
        public decimal DistanceKm { get; init; }
        public decimal FieldQualityBase { get; init; }

        public static FieldDto FromDomain(Field field) => new()
        {
            Id = field.Id.Value,
            AreaHa = field.Area.Value,
            DistanceKm = field.DistanceKm,
            FieldQualityBase = field.FieldQualityBase
        };

        public Field ToDomain() => new()
        {
            Id = new EntityId(Id),
            Area = new AreaHa(AreaHa),
            DistanceKm = DistanceKm,
            FieldQualityBase = FieldQualityBase
        };
    }

    private sealed class TransactionDto
    {
        public Guid TransactionId { get; init; }
        public DateTime GameDateTime { get; init; }
        public required string Type { get; init; }
        public decimal CashDelta { get; init; }
        public Guid? CounterpartyId { get; init; }
        public required List<Guid> RelatedEntityIds { get; init; }
        public required string DescriptionKey { get; init; }

        public static TransactionDto FromDomain(TransactionRecord transaction) => new()
        {
            TransactionId = transaction.TransactionId.Value,
            GameDateTime = transaction.GameDateTime.Value,
            Type = transaction.Type.Value,
            CashDelta = transaction.CashDelta.Value,
            CounterpartyId = transaction.CounterpartyId?.Value,
            RelatedEntityIds = transaction.RelatedEntityIds.Select(id => id.Value).ToList(),
            DescriptionKey = transaction.DescriptionKey.Value
        };

        public TransactionRecord ToDomain() => new()
        {
            TransactionId = new EntityId(TransactionId),
            GameDateTime = new GameDateTime(GameDateTime),
            Type = new CatalogId(Type),
            CashDelta = new Money(CashDelta),
            CounterpartyId = CounterpartyId is { } id ? new EntityId(id) : null,
            RelatedEntityIds = RelatedEntityIds.Select(id => new EntityId(id)).ToArray(),
            DescriptionKey = new CatalogId(DescriptionKey)
        };
    }

    private sealed class DomainEventDto
    {
        public Guid EventId { get; init; }
        public DateTime GameDateTime { get; init; }
        public required string EventType { get; init; }
        public required List<Guid> EntityIds { get; init; }
        public required SortedDictionary<string, string> Payload { get; init; }
        public Guid CausationCommandId { get; init; }
        public Guid CorrelationId { get; init; }

        public static DomainEventDto FromDomain(DomainEvent domainEvent) => new()
        {
            EventId = domainEvent.EventId.Value,
            GameDateTime = domainEvent.GameDateTime.Value,
            EventType = domainEvent.EventType.Value,
            EntityIds = domainEvent.EntityIds.Select(id => id.Value).ToList(),
            Payload = CreateSortedPayload(domainEvent.Payload.Values),
            CausationCommandId = domainEvent.CausationCommandId.Value,
            CorrelationId = domainEvent.CorrelationId.Value
        };

        private static SortedDictionary<string, string> CreateSortedPayload(IReadOnlyDictionary<string, string> values)
        {
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in values)
            {
                result.Add(pair.Key, pair.Value);
            }

            return result;
        }

        public DomainEvent ToDomain()
        {
            var payload = new DomainEventPayload();
            foreach (var pair in Payload)
            {
                payload.Add(pair.Key, pair.Value);
            }

            return new DomainEvent
            {
                EventId = new EntityId(EventId),
                GameDateTime = new GameDateTime(GameDateTime),
                EventType = new CatalogId(EventType),
                EntityIds = EntityIds.Select(id => new EntityId(id)).ToArray(),
                Payload = payload,
                CausationCommandId = new EntityId(CausationCommandId),
                CorrelationId = new EntityId(CorrelationId)
            };
        }
    }
}

public sealed class AutosaveStore
{
    public const string SlotFileName = "autosave.ge-save";

    private readonly string _directory;
    private readonly Func<DateTime> _utcNow;

    public AutosaveStore(string directory, Func<DateTime>? utcNow = null)
    {
        _directory = string.IsNullOrWhiteSpace(directory)
            ? throw new ArgumentException("Save directory cannot be blank.", nameof(directory))
            : directory;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public string SlotPath => Path.Combine(_directory, SlotFileName);

    public SaveHeader Save(GameState state, ConfigVersion configVersion, string buildVersion)
    {
        Directory.CreateDirectory(_directory);

        var now = DateTime.SpecifyKind(_utcNow(), DateTimeKind.Utc);
        var createdAt = now;
        if (File.Exists(SlotPath))
        {
            try
            {
                createdAt = Load().Header.CreatedAtRealTime;
            }
            catch (InvalidDataException)
            {
                // Keep the invalid active file untouched until a fully valid replacement is ready.
            }
        }

        var json = SaveV1Serializer.Serialize(state, configVersion, buildVersion, createdAt, now);
        _ = SaveV1Serializer.Deserialize(json);

        var tempPath = SlotPath + ".tmp";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            var verified = SaveV1Serializer.Deserialize(File.ReadAllText(tempPath, Encoding.UTF8));
            File.Move(tempPath, SlotPath, overwrite: true);
            return verified.Header;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public SaveLoadResult Load()
    {
        if (!File.Exists(SlotPath))
        {
            throw new FileNotFoundException("Autosave slot does not exist.", SlotPath);
        }

        return SaveV1Serializer.Deserialize(File.ReadAllText(SlotPath, Encoding.UTF8));
    }
}
