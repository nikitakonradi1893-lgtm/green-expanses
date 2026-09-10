using System.Text.Json;
using GreenExpanses.Domain;

namespace GreenExpanses.Data;

public enum ConfigIssueSeverity
{
    Warning,
    Error
}

public sealed record ConfigIssue(ConfigIssueSeverity Severity, string Code, string Message, string Path);

public sealed record CatalogDocument(CatalogId Id, string SchemaVersion, JsonElement Root);

public sealed class RuntimeConfig
{
    public required ConfigVersion Version { get; init; }
    public required IReadOnlyDictionary<CatalogId, CatalogDocument> Catalogs { get; init; }
}

public sealed class ConfigLoadResult
{
    public RuntimeConfig? Config { get; init; }
    public required IReadOnlyList<ConfigIssue> Issues { get; init; }

    public bool IsSuccess => Config is not null && Issues.All(static issue => issue.Severity != ConfigIssueSeverity.Error);
}
