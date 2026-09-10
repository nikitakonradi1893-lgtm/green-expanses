namespace GreenExpanses.Domain;

public sealed class GameState
{
    public required CampaignState Campaign { get; init; }
    public required WorldState World { get; init; }
    public required FarmState Farm { get; init; }
    public required EconomyState Economy { get; init; }
    public required SimulationState Simulation { get; init; }

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
    public List<EntityId> FieldIds { get; init; } = [];
}

public sealed class FarmState
{
    public required EntityId PlayerFarmId { get; init; }
    public List<EntityId> OwnedFieldIds { get; init; } = [];
}

public sealed class EconomyState
{
    public Money Cash { get; set; } = Money.Zero;
    public Money Debt { get; set; } = Money.Zero;
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
