using System.Globalization;
using GreenExpanses.Data;
using GreenExpanses.Simulation;

var configPath = ReadOptionalString(args, "--validate-config");
if (configPath is not null)
{
    var result = FileSystemConfigLoader.Load(configPath);
    foreach (var issue in result.Issues
                 .OrderBy(static issue => issue.Severity)
                 .ThenBy(static issue => issue.Code, StringComparer.Ordinal)
                 .ThenBy(static issue => issue.Path, StringComparer.Ordinal))
    {
        Console.WriteLine($"{issue.Severity.ToString().ToLowerInvariant()}:{issue.Code}:{issue.Path}:{issue.Message}");
    }

    if (result.Config is null || result.Issues.Any(static issue => issue.Severity == ConfigIssueSeverity.Error))
    {
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine($"config_version={result.Config.Version}");
    Console.WriteLine($"catalogs={result.Config.Catalogs.Count.ToString(CultureInfo.InvariantCulture)}");
    return;
}

var seed = ReadUnsignedLong(args, "--seed", 1UL);
var days = ReadInt(args, "--days", 0);
var difficulty = ReadString(args, "--difficulty", "normal");

var headlessResult = HeadlessSimulation.Run(new HeadlessRunRequest(seed, days, difficulty));
Console.Write(headlessResult.CanonicalDiagnostics);

static string? ReadOptionalString(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    if (index < 0)
    {
        return null;
    }

    if (index + 1 >= args.Length)
    {
        throw new ArgumentException($"Missing value for {name}.");
    }

    return args[index + 1];
}

static string ReadString(string[] args, string name, string fallback)
{
    return ReadOptionalString(args, name) ?? fallback;
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
