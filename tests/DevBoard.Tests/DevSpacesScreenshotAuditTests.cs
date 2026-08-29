using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DevBoard.Tests;

public class DevSpacesScreenshotAuditTests
{
    [Fact]
    public void Audit_FindsCatalogScenarioForForkOwnedPath()
    {
        var matches = DevSpacesScreenshotAudit.FindScenariosForPath("src/Views/DevSpacesFiles.axaml");
        Assert.Contains(matches, x => x.Id == "files-explorer");
    }

    [Fact]
    public void Audit_IgnoresUnrelatedUpstreamPath()
    {
        Assert.Empty(DevSpacesScreenshotAudit.FindScenariosForPath("src/Views/CommitDetail.axaml"));
    }

    [Fact]
    public void Manifest_IsStableAndOrdered()
    {
        var manifest = DevSpacesScreenshotAudit.BuildManifest(
            DevSpacesScreenshotCatalog.All.Reverse(),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["terminal-main"] = "terminal/terminal-main.png",
                ["files-explorer"] = "files/files-explorer.png",
            });

        Assert.Equal(DevSpacesScreenshotCatalog.All.OrderBy(x => x.Id).Select(x => x.Id), manifest.Select(x => x.Id));
    }
}
