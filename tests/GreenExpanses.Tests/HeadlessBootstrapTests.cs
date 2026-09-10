using GreenExpanses.Simulation;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class HeadlessBootstrapTests
{
    [Fact]
    public void SameRequestProducesIdenticalCanonicalDiagnostics()
    {
        var request = new HeadlessRunRequest(20260910UL, 365, "normal");

        var first = HeadlessSimulation.Run(request);
        var second = HeadlessSimulation.Run(request);

        Assert.Equal(first.CanonicalDiagnostics, second.CanonicalDiagnostics);
        Assert.Equal(first.State.CurrentDateTime, second.State.CurrentDateTime);
        Assert.Equal(first.State.RngState.Streams, second.State.RngState.Streams);
    }

    [Fact]
    public void RunCreatesCampaignAndAdvancesRequestedDays()
    {
        var result = HeadlessSimulation.Run(new HeadlessRunRequest(42UL, 30, "heritage"));

        Assert.Equal(42UL, result.State.WorldSeed);
        Assert.Equal("heritage", result.State.DifficultyProfileId.Value);
        Assert.Equal(30, result.State.Simulation.CompletedDays);
        Assert.Contains("completed_days=30", result.CanonicalDiagnostics);
        Assert.Contains("seed=42", result.CanonicalDiagnostics);
    }

    [Fact]
    public void NegativeDaysAreRejectedBeforeCampaignExecution()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HeadlessSimulation.Run(new HeadlessRunRequest(1UL, -1)));
    }
}
