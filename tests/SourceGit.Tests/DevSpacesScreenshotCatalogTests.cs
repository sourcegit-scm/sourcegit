using System;
using System.Linq;
using Xunit;

namespace SourceGit.Tests;

public class DevSpacesScreenshotCatalogTests
{
    [Fact]
    public void Catalog_HasUniqueScenarioIds()
    {
        var ids = DevSpacesScreenshotCatalog.All.Select(x => x.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Catalog_CoversRequiredForkFeatureGroups()
    {
        var categories = DevSpacesScreenshotCatalog.All.Select(x => x.Category).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("terminal", categories);
        Assert.Contains("files", categories);
        Assert.Contains("navigation", categories);
        Assert.Contains("workspace", categories);
    }

    [Fact]
    public void Catalog_OnlyReferencesForkOwnedFeaturePaths()
    {
        Assert.All(DevSpacesScreenshotCatalog.All, scenario =>
        {
            Assert.NotEmpty(scenario.SourcePaths);
            Assert.All(scenario.SourcePaths, path => Assert.StartsWith("src/", path, StringComparison.Ordinal));
        });
    }

    [Fact]
    public void Catalog_CoversFilesAndCtrlP()
    {
        Assert.Contains(DevSpacesScreenshotCatalog.All, x => x.Id == "files-explorer");
        Assert.Contains(DevSpacesScreenshotCatalog.All, x => x.Id == "ctrl-p-go-to-file");
    }
}
