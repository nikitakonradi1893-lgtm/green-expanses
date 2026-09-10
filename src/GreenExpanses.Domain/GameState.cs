namespace GreenExpanses.Domain;

public sealed class GameState
{
    public required Guid CampaignId { get; init; }
    public required ulong WorldSeed { get; init; }
    public required DateTime CurrentDateTime { get; set; }
    public required string DifficultyProfileId { get; init; }
    public required Guid PlayerFarmId { get; init; }
    public required RngState RngState { get; init; }
}

public sealed class RngState
{
    public Dictionary<string, ulong> Streams { get; init; } = new(StringComparer.Ordinal);
}
