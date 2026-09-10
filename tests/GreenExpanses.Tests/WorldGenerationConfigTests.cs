using GreenExpanses.Data;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class WorldGenerationConfigTests
{
    [Fact]
    public void BundledConfigMatchesApprovedWorld001Totals()
    {
        var config = WorldGenerationConfigLoader.LoadBundled();

        Assert.Equal(500, config.FieldCount);
        Assert.Equal(config.FieldCount, config.AreaBands.Sum(static band => band.Count));
        Assert.Equal(config.FieldCount, config.DistanceBands.Sum(static band => band.Count));
        Assert.Equal(100, config.QualityBands.Sum(static band => band.WeightPercent));
    }

    [Fact]
    public void FileSystemRuntimeConfigContainsWorldGenerationCatalog()
    {
        var root = FindConfigRoot();
        var runtime = FileSystemConfigLoader.Load(root);

        Assert.True(runtime.IsSuccess);
        var typed = WorldGenerationConfigLoader.Load(runtime.Config!);
        var bundled = WorldGenerationConfigLoader.LoadBundled();

        Assert.Equal(bundled.Id, typed.Id);
        Assert.Equal(bundled.FieldCount, typed.FieldCount);
        Assert.Equal(bundled.AreaBands, typed.AreaBands);
        Assert.Equal(bundled.DistanceBands, typed.DistanceBands);
        Assert.Equal(bundled.QualityBands, typed.QualityBands);
    }

    private static string FindConfigRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data", "config", "manifest.json");
            if (File.Exists(candidate))
            {
                return Path.GetDirectoryName(candidate)!;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate data/config from test output.");
    }
}
