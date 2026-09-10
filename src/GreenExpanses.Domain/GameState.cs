namespace GreenExpanses.Domain;

public sealed class GameState
{
    public required CampaignState Campaign { get; init; }
    public required WorldState World { get; init; }
    public required FarmState Farm { get; init; }
    public required EconomyState Economy { get; init; }
    public required SimulationState Simulation { get; init; }
    public required EventLog EventLog { get; init; }

    public EntityId CampaignId => Campaign.Id;
    public ulong WorldSeed => Campaign.WorldSeed;
    public CatalogId DifficultyProfileId => Campaign.DifficultyProfileId;
    public EntityId PlayerFarmId => Farm.PlayerFarmId;
    public GameDateTime CurrentDateTime
    {
        get => Simulation.CurrentDateTime;
        set => Simulation.CurrentDateTime = value;
    }

    public RngState RngState
    {
        get => Simulation.RngState;
        set => Simulation.RngState = value;
    }
}

public sealed class CampaignState
{
    public required EntityId Id { get; init; }
    public required ulong WorldSeed { get; init; }
    public required CatalogId DifficultyProfileId { get; init; }
}

public sealed class WorldState
{
    private readonly List<Field> _fields = [];

    public EntityRegistry FieldRegistry { get; init; } = new();
    public IReadOnlyCollection<EntityId> FieldIds => FieldRegistry.Ids;
    public IReadOnlyList<Field> Fields => _fields;

    public void AddField(Field field)
    {
        ArgumentNullException.ThrowIfNull(field);
        field.Validate();
        FieldRegistry.Add(field.Id);
        _fields.Add(field);
    }
}

public sealed class FarmState
{
    public required EntityId PlayerFarmId { get; init; }
    public List<EntityId> OwnedFieldIds { get; init; } = [];
}

public sealed class EconomyState
{
    public EconomyState() : this(Money.Zero)
    {
    }

    public EconomyState(Money openingCash)
    {
        OpeningCash = openingCash;
        Cash = openingCash;
    }

    public Money OpeningCash { get; }
    public Money Cash { get; private set; }
    public Money Debt { get; set; } = Money.Zero;
    public TransactionLedger Ledger { get; } = new();

    public void PostTransaction(TransactionRecord transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        Ledger.Append(transaction);
        Cash += transaction.CashDelta;
    }
}

public sealed class SimulationState
{
    public required GameDateTime CurrentDateTime { get; set; }
    public required RngState RngState { get; set; }
    public long CompletedDays { get; set; }
}

public sealed class RngState
{
    public Dictionary<string, ulong> Streams { get; init; } = new(StringComparer.Ordinal);
}
