using System.Text.Json;
using GreenExpanses.Domain;
using GreenExpanses.Infrastructure;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class RngContractTests
{
    [Fact]
    public void Snapshot_RestoresExactContinuation()
    {
        var rng = new DeterministicRngService(42UL);
        _ = rng.NextUInt64(RngStreams.Weather);
        _ = rng.NextUInt64(RngStreams.Market);
        var snapshot = rng.Snapshot();

        var expected = new[]
        {
            rng.NextUInt64(RngStreams.Weather),
            rng.NextUInt64(RngStreams.Weather),
            rng.NextUInt64(RngStreams.Market)
        };

        var restored = new DeterministicRngService(42UL, snapshot);
        var actual = new[]
        {
            restored.NextUInt64(RngStreams.Weather),
            restored.NextUInt64(RngStreams.Weather),
            restored.NextUInt64(RngStreams.Market)
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Snapshot_IsIndependentCopy()
    {
        var rng = new DeterministicRngService(42UL);
        var snapshot = rng.Snapshot();
        var before = snapshot.Streams[RngStreams.Weather];

        _ = rng.NextUInt64(RngStreams.Weather);

        Assert.Equal(before, snapshot.Streams[RngStreams.Weather]);
    }

    [Fact]
    public void NamedStreams_AreIndependent()
    {
        var baseline = new DeterministicRngService(100UL);
        var expectedMarket = baseline.NextUInt64(RngStreams.Market);

        var perturbed = new DeterministicRngService(100UL);
        for (var i = 0; i < 100; i++)
        {
            _ = perturbed.NextUInt64(RngStreams.Weather);
        }

        Assert.Equal(expectedMarket, perturbed.NextUInt64(RngStreams.Market));
    }

    [Fact]
    public void State_RoundTripsThroughJson()
    {
        var rng = new DeterministicRngService(20260910UL);
        _ = rng.NextUInt64(RngStreams.Failures);
        _ = rng.NextUInt64(RngStreams.Quality);
        var snapshot = rng.Snapshot();

        var json = JsonSerializer.Serialize(snapshot);
        var restoredState = JsonSerializer.Deserialize<RngState>(json);

        Assert.NotNull(restoredState);
        var restored = new DeterministicRngService(20260910UL, restoredState);
        Assert.Equal(rng.NextUInt64(RngStreams.Failures), restored.NextUInt64(RngStreams.Failures));
        Assert.Equal(rng.NextUInt64(RngStreams.Quality), restored.NextUInt64(RngStreams.Quality));
    }

    [Fact]
    public void NextInt_StaysWithinRequestedRange()
    {
        var rng = new DeterministicRngService(7UL);
        for (var i = 0; i < 500; i++)
        {
            var value = rng.NextInt(RngStreams.Land, -3, 8);
            Assert.InRange(value, -3, 7);
        }
    }

    [Fact]
    public void UnknownStream_IsRejected()
    {
        var rng = new DeterministicRngService(1UL);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextUInt64("not_a_stream"));
    }
}
