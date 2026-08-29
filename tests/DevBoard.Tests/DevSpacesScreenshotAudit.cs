using System;
using System.Collections.Generic;
using System.Linq;

namespace DevBoard.Tests;

public sealed record DevSpacesScreenshotManifestEntry(string Id, string Title, string Category, string? Path);

public static class DevSpacesScreenshotAudit
{
    public static IReadOnlyList<DevSpacesScreenshotScenario> FindScenariosForPath(string path) =>
        DevSpacesScreenshotCatalog.All
            .Where(x => x.SourcePaths.Contains(path, StringComparer.Ordinal))
            .OrderBy(x => x.Id, StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyList<DevSpacesScreenshotManifestEntry> BuildManifest(
        IEnumerable<DevSpacesScreenshotScenario> scenarios,
        IReadOnlyDictionary<string, string> screenshots) =>
        scenarios
            .OrderBy(x => x.Id, StringComparer.Ordinal)
            .Select(x => new DevSpacesScreenshotManifestEntry(
                x.Id,
                x.Title,
                x.Category,
                screenshots.TryGetValue(x.Id, out var path) ? path : null))
            .ToArray();
}
