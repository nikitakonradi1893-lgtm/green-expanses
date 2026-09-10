namespace GreenExpanses.Domain;

public sealed class GameState
{
    public required EntityId CampaignId { get; init; }
    public required ulong WorldSeed { get; init; }
    public required GameDateTime CurrentDateTime { get; set; }
    public required CatalogId DifficultyProfileId { get; init; }
    public required EntityId PlayerFarmId { get; init; }
    public required RngState RngState { get; init; }
}

public sealed class RngState
{
    public Dictionary<string, ulong> Streams { get; init; } = new(StringComparer.Ordinal);
}
