using System;
using System.IO;

using DevBoard.Native;
using Xunit;

namespace DevBoard.Tests.Native;

public sealed class DataDirectoryResolverTests
{
    [Fact]
    public void NoExistingData_UsesDevBoardPath()
    {
        using var fs = new TempDirectories();
        var portable = fs.PathOf("portable");
        var devBoard = fs.PathOf("DevBoard");
        var legacy = fs.PathOf("SourceGit");

        var actual = DataDirectoryResolver.Resolve(portable, devBoard, legacy);

        Assert.Equal(devBoard, actual);
    }

    [Fact]
    public void LegacyOnly_IsCopiedToDevBoard_AndLegacyRemains()
    {
        using var fs = new TempDirectories();
        var portable = fs.PathOf("portable");
        var devBoard = fs.PathOf("DevBoard");
        var legacy = fs.PathOf("SourceGit");
        Directory.CreateDirectory(Path.Combine(legacy, "nested"));
        File.WriteAllText(Path.Combine(legacy, "settings.json"), "legacy-settings");
        File.WriteAllText(Path.Combine(legacy, "nested", "state.json"), "legacy-state");

        var actual = DataDirectoryResolver.Resolve(portable, devBoard, legacy);

        Assert.Equal(devBoard, actual);
        Assert.Equal("legacy-settings", File.ReadAllText(Path.Combine(devBoard, "settings.json")));
        Assert.Equal("legacy-state", File.ReadAllText(Path.Combine(devBoard, "nested", "state.json")));
        Assert.True(File.Exists(Path.Combine(legacy, "settings.json")));
        Assert.True(File.Exists(Path.Combine(legacy, "nested", "state.json")));
    }

    [Fact]
    public void ExistingDevBoardData_WinsWithoutOverwrite()
    {
        using var fs = new TempDirectories();
        var portable = fs.PathOf("portable");
        var devBoard = fs.PathOf("DevBoard");
        var legacy = fs.PathOf("SourceGit");
        Directory.CreateDirectory(devBoard);
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(devBoard, "settings.json"), "devboard-settings");
        File.WriteAllText(Path.Combine(legacy, "settings.json"), "legacy-settings");
        File.WriteAllText(Path.Combine(legacy, "legacy-only.json"), "legacy-only");

        var actual = DataDirectoryResolver.Resolve(portable, devBoard, legacy);

        Assert.Equal(devBoard, actual);
        Assert.Equal("devboard-settings", File.ReadAllText(Path.Combine(devBoard, "settings.json")));
        Assert.False(File.Exists(Path.Combine(devBoard, "legacy-only.json")));
        Assert.Equal("legacy-settings", File.ReadAllText(Path.Combine(legacy, "settings.json")));
    }

    [Fact]
    public void PortableData_WinsAndBypassesMigration()
    {
        using var fs = new TempDirectories();
        var portable = fs.PathOf("portable");
        var devBoard = fs.PathOf("DevBoard");
        var legacy = fs.PathOf("SourceGit");
        Directory.CreateDirectory(portable);
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(portable, "settings.json"), "portable-settings");
        File.WriteAllText(Path.Combine(legacy, "settings.json"), "legacy-settings");

        var actual = DataDirectoryResolver.Resolve(portable, devBoard, legacy);

        Assert.Equal(portable, actual);
        Assert.False(Directory.Exists(devBoard));
        Assert.Equal("legacy-settings", File.ReadAllText(Path.Combine(legacy, "settings.json")));
    }

    [Fact]
    public void MigrationFailure_FallsBackToLegacyAndLogs()
    {
        using var fs = new TempDirectories();
        var portable = fs.PathOf("portable");
        var devBoard = fs.PathOf("DevBoard");
        var legacy = fs.PathOf("SourceGit");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "settings.json"), "legacy-settings");
        File.WriteAllText(devBoard, "blocks-directory-creation");
        string? log = null;

        var actual = DataDirectoryResolver.Resolve(portable, devBoard, legacy, message => log = message);

        Assert.Equal(legacy, actual);
        Assert.NotNull(log);
        Assert.Contains("legacy", log!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("legacy-settings", File.ReadAllText(Path.Combine(legacy, "settings.json")));
    }

    private sealed class TempDirectories : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"devboard-data-migration-{Guid.NewGuid():N}");

        public TempDirectories()
        {
            Directory.CreateDirectory(_root);
        }

        public string PathOf(string name) => Path.Combine(_root, name);

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
    }
}
