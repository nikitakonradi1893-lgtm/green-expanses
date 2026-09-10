using GreenExpanses.Data;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class ConfigLoaderTests
{
    [Fact]
    public void ValidConfig_LoadsVersionedCatalog()
    {
        using var fixture = new ConfigFixture();
        fixture.Write("manifest.json", """
            {"configVersion":"1.0.0","catalogs":[{"id":"crops","file":"crops.json","schemaVersion":"1"}]}
            """);
        fixture.Write("crops.json", """
            {"schemaVersion":"1","items":[{"id":"winter_wheat"}]}
            """);

        var result = FileSystemConfigLoader.Load(fixture.Path);

        Assert.True(result.IsSuccess);
        Assert.Equal("1.0.0", result.Config!.Version.Value);
        Assert.Single(result.Config.Catalogs);
    }

    [Fact]
    public void SchemaVersionMismatch_IsHardError()
    {
        using var fixture = new ConfigFixture();
        fixture.Write("manifest.json", """
            {"configVersion":"1.0.0","catalogs":[{"id":"crops","file":"crops.json","schemaVersion":"2"}]}
            """);
        fixture.Write("crops.json", """
            {"schemaVersion":"1","items":[]}
            """);

        var result = FileSystemConfigLoader.Load(fixture.Path);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, static issue => issue.Code == "config.schema_version_mismatch" && issue.Severity == ConfigIssueSeverity.Error);
    }

    [Fact]
    public void EmptyCatalog_IsSoftWarning()
    {
        using var fixture = new ConfigFixture();
        fixture.Write("manifest.json", """
            {"configVersion":"1.0.0","catalogs":[{"id":"crops","file":"crops.json","schemaVersion":"1"}]}
            """);
        fixture.Write("crops.json", """
            {"schemaVersion":"1","items":[]}
            """);

        var result = FileSystemConfigLoader.Load(fixture.Path);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Issues, static issue => issue.Code == "config.catalog_empty" && issue.Severity == ConfigIssueSeverity.Warning);
    }

    [Fact]
    public void MissingCatalogFile_StopsLoad()
    {
        using var fixture = new ConfigFixture();
        fixture.Write("manifest.json", """
            {"configVersion":"1.0.0","catalogs":[{"id":"crops","file":"missing.json","schemaVersion":"1"}]}
            """);

        var result = FileSystemConfigLoader.Load(fixture.Path);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Contains(result.Issues, static issue => issue.Code == "config.catalog_missing");
    }

    private sealed class ConfigFixture : IDisposable
    {
        public ConfigFixture()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "green-expanses-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Write(string relativePath, string content)
        {
            File.WriteAllText(System.IO.Path.Combine(Path, relativePath), content);
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
