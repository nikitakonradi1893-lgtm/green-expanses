using System.Text.Json;
using GreenExpanses.Domain;

namespace GreenExpanses.Data;

public static class FileSystemConfigLoader
{
    private const string ManifestFileName = "manifest.json";

    public static ConfigLoadResult Load(string rootPath)
    {
        Validation.NotBlank(rootPath, nameof(rootPath));
        var issues = new List<ConfigIssue>();
        var manifestPath = Path.Combine(rootPath, ManifestFileName);

        if (!File.Exists(manifestPath))
        {
            return Failure(issues, "config.manifest_missing", "Configuration manifest.json was not found.", manifestPath);
        }

        JsonDocument manifest;
        try
        {
            manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return Failure(issues, "config.manifest_unreadable", exception.Message, manifestPath);
        }

        using (manifest)
        {
            if (manifest.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Failure(issues, "config.manifest_shape", "Manifest root must be a JSON object.", manifestPath);
            }

            if (!TryRequiredString(manifest.RootElement, "configVersion", manifestPath, issues, out var rawVersion))
            {
                return new ConfigLoadResult { Config = null, Issues = issues };
            }

            ConfigVersion version;
            try
            {
                version = new ConfigVersion(rawVersion!);
            }
            catch (ArgumentException exception)
            {
                issues.Add(new ConfigIssue(ConfigIssueSeverity.Error, "config.version_invalid", exception.Message, manifestPath));
                return new ConfigLoadResult { Config = null, Issues = issues };
            }

            if (!manifest.RootElement.TryGetProperty("catalogs", out var catalogsElement) || catalogsElement.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ConfigIssue(ConfigIssueSeverity.Error, "config.catalogs_missing", "Manifest catalogs must be a JSON array.", manifestPath));
                return new ConfigLoadResult { Config = null, Issues = issues };
            }

            var catalogs = new Dictionary<CatalogId, CatalogDocument>();
            foreach (var catalogEntry in catalogsElement.EnumerateArray())
            {
                LoadCatalog(rootPath, catalogEntry, catalogs, issues);
            }

            if (catalogs.Count == 0)
            {
                issues.Add(new ConfigIssue(ConfigIssueSeverity.Warning, "config.no_catalogs", "No runtime catalogs are registered yet.", manifestPath));
            }

            if (issues.Any(static issue => issue.Severity == ConfigIssueSeverity.Error))
            {
                return new ConfigLoadResult { Config = null, Issues = issues };
            }

            return new ConfigLoadResult
            {
                Config = new RuntimeConfig { Version = version, Catalogs = catalogs },
                Issues = issues
            };
        }
    }

    private static void LoadCatalog(
        string rootPath,
        JsonElement entry,
        Dictionary<CatalogId, CatalogDocument> catalogs,
        List<ConfigIssue> issues)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ConfigIssue(ConfigIssueSeverity.Error, "config.catalog_entry_shape", "Catalog entry must be an object.", ManifestFileName));
            return;
        }

        if (!TryRequiredString(entry, "id", ManifestFileName, issues, out var rawId) ||
            !TryRequiredString(entry, "file", ManifestFileName, issues, out var fileName) ||
            !TryRequiredString(entry, "schemaVersion", ManifestFileName, issues, out var expectedSchemaVersion))
        {
            return;
        }

        CatalogId catalogId;
        try
        {
            catalogId = new CatalogId(rawId!);
        }
        catch (ArgumentException exception)
        {
            issues.Add(new ConfigIssue(ConfigIssueSeverity.Error, "config.catalog_id_invalid", exception.Message, ManifestFileName));
            return;
        }

        if (catalogs.ContainsKey(catalogId))
        {
            issues.Add(new ConfigIssue(ConfigIssueSeverity.Error, "config.catalog_duplicate", $"Duplicate catalog '{catalogId}'.", ManifestFileName));
            return;
        }

        var fullPath = Path.GetFullPath(Path.Combine(rootPath, fileName!));
        var rootFullPath = Path.GetFullPath(rootPath) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootFullPath, StringComparison.Ordinal))
        {
            issues.Add(new ConfigIssue(ConfigIssueSeverity.Error, "config.catalog_path_escape", "Catalog path escapes the config root.", fileName!));
            return;
        }

        if (!File.Exists(fullPath))
        {
            issues.Add(new ConfigIssue(ConfigIssueSeverity.Error, "config.catalog_missing", $"Catalog file '{fileName}' was not found.", fullPath));
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(fullPath));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ConfigIssue(ConfigIssueSeverity.Error, "config.catalog_shape", "Catalog root must be an object.", fullPath));
                return;
            }

            if (!TryRequiredString(document.RootElement, "schemaVersion", fullPath, issues, out var actualSchemaVersion))
            {
                return;
            }

            if (!string.Equals(actualSchemaVersion, expectedSchemaVersion, StringComparison.Ordinal))
            {
                issues.Add(new ConfigIssue(ConfigIssueSeverity.Error, "config.schema_version_mismatch", $"Expected schemaVersion '{expectedSchemaVersion}', got '{actualSchemaVersion}'.", fullPath));
                return;
            }

            if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ConfigIssue(ConfigIssueSeverity.Error, "config.catalog_items_missing", "Catalog must contain an items array.", fullPath));
                return;
            }

            if (items.GetArrayLength() == 0)
            {
                issues.Add(new ConfigIssue(ConfigIssueSeverity.Warning, "config.catalog_empty", $"Catalog '{catalogId}' is empty.", fullPath));
            }

            catalogs.Add(catalogId, new CatalogDocument(catalogId, actualSchemaVersion!, document.RootElement.Clone()));
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            issues.Add(new ConfigIssue(ConfigIssueSeverity.Error, "config.catalog_unreadable", exception.Message, fullPath));
        }
    }

    private static bool TryRequiredString(JsonElement parent, string propertyName, string path, List<ConfigIssue> issues, out string? value)
    {
        value = null;
        if (!parent.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
        {
            issues.Add(new ConfigIssue(ConfigIssueSeverity.Error, "config.required_string", $"Required string '{propertyName}' is missing or blank.", path));
            return false;
        }

        value = element.GetString();
        return true;
    }

    private static ConfigLoadResult Failure(List<ConfigIssue> issues, string code, string message, string path)
    {
        issues.Add(new ConfigIssue(ConfigIssueSeverity.Error, code, message, path));
        return new ConfigLoadResult { Config = null, Issues = issues };
    }
}
