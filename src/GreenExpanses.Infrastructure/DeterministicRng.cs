using GreenExpanses.Domain;

namespace GreenExpanses.Infrastructure;

public static class RngStreams
{
    public const string Weather = "weather";
    public const string Market = "market";
    public const string Land = "land";
    public const string Contractors = "contractors";
    public const string UsedMachinery = "used_machinery";
    public const string Failures = "failures";
    public const string Quality = "quality";

    public static readonly string[] All =
    [
        Weather,
        Market,
        Land,
        Contractors,
        UsedMachinery,
        Failures,
        Quality
    ];
}

public interface IDeterministicRng
{
    ulong NextUInt64(string streamName);
    double NextUnitDouble(string streamName);
    int NextInt(string streamName, int minInclusive, int maxExclusive);
    RngState Snapshot();
}

public sealed class DeterministicRngService : IDeterministicRng
{
    private readonly ulong _worldSeed;
    private readonly RngState _state;

    public DeterministicRngService(ulong worldSeed, RngState? state = null)
    {
        _worldSeed = worldSeed;
        _state = state is null ? CreateInitialState(worldSeed) : CloneState(state);
        EnsureKnownStreams();
    }

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

    public int NextInt(string streamName, int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "maxExclusive must be greater than minInclusive.");
        }

        var range = (ulong)(maxExclusive - minInclusive);
        var threshold = unchecked((0UL - range) % range);
        ulong value;
        do
        {
            value = NextUInt64(streamName);
        }
        while (value < threshold);

        return minInclusive + (int)(value % range);
    }

    public RngState Snapshot() => CloneState(_state);

    public static RngState CreateInitialState(ulong worldSeed)
    {
        var state = new RngState();
        foreach (var stream in RngStreams.All)
        {
            state.Streams[stream] = MixSeed(worldSeed, stream);
        }

        return state;
    }

    public static RngState CloneState(RngState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new RngState
        {
            Streams = new Dictionary<string, ulong>(source.Streams, StringComparer.Ordinal)
        };
    }

    private void EnsureKnownStreams()
    {
        foreach (var stream in RngStreams.All)
        {
            _state.Streams.TryAdd(stream, MixSeed(_worldSeed, stream));
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
