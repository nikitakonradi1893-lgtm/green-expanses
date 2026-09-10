using System.Globalization;
using GreenExpanses.Simulation;

var seed = ReadUnsignedLong(args, "--seed", 1UL);
var days = ReadInt(args, "--days", 0);
var difficulty = ReadString(args, "--difficulty", "normal");

var state = GameStateFactory.Create(seed, difficulty);
EmptyDailySimulation.Advance(state, days);
Console.Write(CanonicalDiagnostics.Serialize(state));

static string ReadString(string[] args, string name, string fallback)
{
    var index = Array.IndexOf(args, name);
    if (index < 0)
    {
        return fallback;
    }

    if (index + 1 >= args.Length)
    {
        throw new ArgumentException($"Missing value for {name}.");
    }

    return args[index + 1];
}

static ulong ReadUnsignedLong(string[] args, string name, ulong fallback)
{
    var value = ReadString(args, name, fallback.ToString(CultureInfo.InvariantCulture));
    return ulong.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
}

static int ReadInt(string[] args, string name, int fallback)
{
    var value = ReadString(args, name, fallback.ToString(CultureInfo.InvariantCulture));
    return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
}
