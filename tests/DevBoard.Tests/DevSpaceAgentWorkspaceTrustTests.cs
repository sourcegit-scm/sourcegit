using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using DevBoard.DevSpaces;
using Xunit;

namespace DevBoard.Tests;

public sealed class DevSpaceAgentWorkspaceTrustTests
{
    [Fact]
    public void CodexEnsureTrustedAddsProjectAndPreservesExistingConfig()
    {
        var root = CreateTempDirectory();
        var workspace = Path.Combine(root, "workspace");
        var codexHome = Path.Combine(root, ".codex");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(codexHome);
        File.WriteAllText(Path.Combine(codexHome, "config.toml"), "model = \"gpt-5\"\n");

        try
        {
            Assert.True(CodexWorkspaceTrust.EnsureTrusted(workspace, codexHome));

            var config = File.ReadAllText(Path.Combine(codexHome, "config.toml"));
            Assert.Contains("model = \"gpt-5\"", config);
            Assert.Contains($"[projects.\"{EscapeToml(Path.GetFullPath(workspace))}\"]", config);
            Assert.Contains("trust_level = \"trusted\"", config);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CodexEnsureTrustedDoesNotDuplicateProject()
    {
        var root = CreateTempDirectory();
        var workspace = Path.Combine(root, "workspace");
        var codexHome = Path.Combine(root, ".codex");
        Directory.CreateDirectory(workspace);

        try
        {
            Assert.True(CodexWorkspaceTrust.EnsureTrusted(workspace, codexHome));
            Assert.True(CodexWorkspaceTrust.EnsureTrusted(workspace, codexHome));

            var config = File.ReadAllText(Path.Combine(codexHome, "config.toml"));
            var projectHeader = $"[projects.\"{EscapeToml(Path.GetFullPath(workspace))}\"]";
            Assert.Equal(1, CountOccurrences(config, projectHeader));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void AntigravityEnsureTrustedAddsExactWorkspaceAndPreservesExistingSettings()
    {
        var root = CreateTempDirectory();
        var workspace = Path.Combine(root, "workspace");
        var existingWorkspace = Path.Combine(root, "existing");
        var antigravityHome = Path.Combine(root, "antigravity-cli");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(existingWorkspace);
        Directory.CreateDirectory(antigravityHome);
        File.WriteAllText(
            Path.Combine(antigravityHome, "settings.json"),
            JsonSerializer.Serialize(new
            {
                theme = "dark",
                trustedWorkspaces = new[] { Path.GetFullPath(existingWorkspace) },
            }));

        try
        {
            Assert.True(AntigravityWorkspaceTrust.EnsureTrusted(workspace, antigravityHome));

            using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(antigravityHome, "settings.json")));
            Assert.Equal("dark", settings.RootElement.GetProperty("theme").GetString());
            var trusted = settings.RootElement
                .GetProperty("trustedWorkspaces")
                .EnumerateArray()
                .Select(x => x.GetString())
                .ToArray();
            Assert.Contains(Path.GetFullPath(existingWorkspace), trusted);
            Assert.Contains(Path.GetFullPath(workspace), trusted);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void AntigravityEnsureTrustedDoesNotDuplicateWorkspace()
    {
        var root = CreateTempDirectory();
        var workspace = Path.Combine(root, "workspace");
        var antigravityHome = Path.Combine(root, "antigravity-cli");
        Directory.CreateDirectory(workspace);

        try
        {
            Assert.True(AntigravityWorkspaceTrust.EnsureTrusted(workspace, antigravityHome));
            Assert.True(AntigravityWorkspaceTrust.EnsureTrusted(workspace, antigravityHome));

            using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(antigravityHome, "settings.json")));
            var trusted = settings.RootElement
                .GetProperty("trustedWorkspaces")
                .EnumerateArray()
                .Select(x => x.GetString())
                .Where(x => PathsEqual(x, workspace))
                .ToArray();
            Assert.Single(trusted);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"devboard-agent-trust-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string EscapeToml(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static bool PathsEqual(string left, string right)
    {
        if (left == null)
            return false;

        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
