using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using SourceGit.DevSpaces;
using Xunit;

namespace SourceGit.Tests;

public class CopilotWorkspaceTrustTests
{
    [Fact]
    public void EnsureTrusted_AddsWorkspaceAndPreservesExistingConfig()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sourcegit-copilot-trust-{Guid.NewGuid():N}");
        var workspace = Path.Combine(root, "workspace");
        var existingTrusted = Path.Combine(root, "existing");
        var copilotHome = Path.Combine(root, ".copilot");
        var configPath = Path.Combine(copilotHome, "config.json");

        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(existingTrusted);
        Directory.CreateDirectory(copilotHome);
        File.WriteAllText(
            configPath,
            $$"""
            {
              "theme": "dark",
              "trustedFolders": [
                "{{JsonEncodedText.Encode(existingTrusted)}}"
              ]
            }
            """);

        try
        {
            Assert.True(CopilotWorkspaceTrust.EnsureTrusted(workspace, copilotHome));

            using var config = JsonDocument.Parse(File.ReadAllText(configPath));
            Assert.Equal("dark", config.RootElement.GetProperty("theme").GetString());

            var trustedFolders = config.RootElement
                .GetProperty("trustedFolders")
                .EnumerateArray()
                .Select(x => x.GetString())
                .ToArray();

            Assert.Contains(Path.GetFullPath(existingTrusted), trustedFolders);
            Assert.Contains(Path.GetFullPath(workspace), trustedFolders);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void EnsureTrusted_DoesNotDuplicateWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sourcegit-copilot-trust-{Guid.NewGuid():N}");
        var workspace = Path.Combine(root, "workspace");
        var copilotHome = Path.Combine(root, ".copilot");

        Directory.CreateDirectory(workspace);

        try
        {
            Assert.True(CopilotWorkspaceTrust.EnsureTrusted(workspace, copilotHome));
            Assert.True(CopilotWorkspaceTrust.EnsureTrusted(workspace, copilotHome));

            var configPath = Path.Combine(copilotHome, "config.json");
            using var config = JsonDocument.Parse(File.ReadAllText(configPath));
            var trustedFolders = config.RootElement
                .GetProperty("trustedFolders")
                .EnumerateArray()
                .Select(x => x.GetString())
                .Where(x => string.Equals(
                    Path.GetFullPath(x!),
                    Path.GetFullPath(workspace),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                .ToArray();

            Assert.Single(trustedFolders);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
