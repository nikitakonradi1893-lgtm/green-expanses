using GreenExpanses.Domain;

namespace GreenExpanses.Simulation;

public sealed record HeadlessRunRequest(
    ulong WorldSeed,
    int Days,
    string DifficultyProfileId = "normal");

public sealed record HeadlessRunResult(
    GameState State,
    string CanonicalDiagnostics);

public static class HeadlessSimulation
{
    public static HeadlessRunResult Run(HeadlessRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Days < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Headless run days cannot be negative.");
        }

        var state = GameStateFactory.Create(request.WorldSeed, request.DifficultyProfileId);
        EmptyDailySimulation.Advance(state, request.Days);
        return new HeadlessRunResult(state, CanonicalDiagnostics.Serialize(state));
    }
}
