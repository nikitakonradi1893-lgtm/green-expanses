using System.Text.Json;
using GreenExpanses.Persistence;
using GreenExpanses.Simulation;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class Core011VerticalFixtureTests
{
    [Fact]
    public void IdenticalRunsProduceByteEquivalentCanonicalStateAfter365Days()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "core-011-empty-365.json");
        var fixture = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(fixturePath), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException("CORE-011 fixture could not be deserialized.");

        var request = new HeadlessRunRequest(
            fixture.WorldSeed,
            fixture.Days,
            fixture.DifficultyProfileId);

        var first = HeadlessSimulation.Run(request);
        var second = HeadlessSimulation.Run(request);
        var firstBytes = System.Text.Encoding.UTF8.GetBytes(CanonicalStateSnapshot.Serialize(first.State));
        var secondBytes = System.Text.Encoding.UTF8.GetBytes(CanonicalStateSnapshot.Serialize(second.State));

        Assert.Equal(fixture.ExpectedCompletedDays, first.State.Simulation.CompletedDays);
        Assert.True(firstBytes.AsSpan().SequenceEqual(secondBytes));
    }

    [Fact]
    public void DifferentSeedProducesDifferentCanonicalState()
    {
        var first = HeadlessSimulation.Run(new HeadlessRunRequest(20260910UL, 365));
        var second = HeadlessSimulation.Run(new HeadlessRunRequest(20260911UL, 365));

        Assert.NotEqual(
            CanonicalStateSnapshot.Serialize(first.State),
            CanonicalStateSnapshot.Serialize(second.State));
    }

    private sealed class Fixture
    {
        public required string Name { get; init; }
        public ulong WorldSeed { get; init; }
        public int Days { get; init; }
        public required string DifficultyProfileId { get; init; }
        public int ExpectedCompletedDays { get; init; }
        public required string ExpectedBehavior { get; init; }
    }
}
