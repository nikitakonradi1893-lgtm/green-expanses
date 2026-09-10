using GreenExpanses.Domain;

namespace GreenExpanses.Infrastructure;

public static class RngStreams
{
    public static readonly string[] All =
    [
        "weather",
        "market",
        "land",
        "contractors",
        "used_machinery",
        "failures",
        "quality"
    ];
}

public sealed class DeterministicRngService
{
    private readonly RngState _state;

    public DeterministicRngService(ulong worldSeed, RngState? state = null)
    {
        _state = state ?? CreateInitialState(worldSeed);
        EnsureKnownStreams(worldSeed);
    }

    public RngState State => _state;

    public ulong NextUInt64(string streamName)
    {
        if (!_state.Streams.TryGetValue(streamName, out var state))
        {
            throw new ArgumentOutOfRangeException(nameof(streamName), streamName, "Unknown RNG stream.");
        }

        state += 0x9E3779B97F4A7C15UL;
        _state.Streams[streamName] = state;

        var z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public double NextUnitDouble(string streamName)
    {
        return (NextUInt64(streamName) >> 11) * (1.0 / (1UL << 53));
    }

    public static RngState CreateInitialState(ulong worldSeed)
    {
        var state = new RngState();
        foreach (var stream in RngStreams.All)
        {
            state.Streams[stream] = MixSeed(worldSeed, stream);
        }

        return state;
    }

    private void EnsureKnownStreams(ulong worldSeed)
    {
        foreach (var stream in RngStreams.All)
        {
            _state.Streams.TryAdd(stream, MixSeed(worldSeed, stream));
        }
    }

    private static ulong MixSeed(ulong worldSeed, string streamName)
    {
        var hash = 14695981039346656037UL;
        foreach (var ch in streamName)
        {
            hash ^= (byte)ch;
            hash *= 1099511628211UL;
            hash ^= (byte)(ch >> 8);
            hash *= 1099511628211UL;
        }

        var x = worldSeed ^ hash;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }
}
