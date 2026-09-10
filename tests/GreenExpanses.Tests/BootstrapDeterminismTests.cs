using Xunit;
using GreenExpanses.Infrastructure;
using GreenExpanses.Simulation;

namespace GreenExpanses.Tests;

public sealed class BootstrapDeterminismTests
{
    [Fact]
    public void NamedStreams_AreStableAndComplete()
    {
        var first = DeterministicRngService.CreateInitialState(42UL);
        var second = DeterministicRngService.CreateInitialState(42UL);

        Assert.Equal(RngStreams.All.OrderBy(static x => x), first.Streams.Keys.OrderBy(static x => x));
        Assert.Equal(first.Streams.OrderBy(static x => x.Key), second.Streams.OrderBy(static x => x.Key));
    }

    [Fact]
    public void SameSeedAndTicks_ProduceByteEquivalentDiagnostics()
    {
        var first = Run(20260910UL, 365);
        var second = Run(20260910UL, 365);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentDiagnostics()
    {
        Assert.NotEqual(Run(1UL, 365), Run(2UL, 365));
    }

    private static string Run(ulong seed, int days)
    {
        var state = GameStateFactory.Create(seed);
        EmptyDailySimulation.Advance(state, days);
        return CanonicalDiagnostics.Serialize(state);
    }
}
