using System.Linq;
using System.Text.Json;

using SourceGit.DevSpaces;
using Xunit;

namespace SourceGit.Tests;

public sealed class DevSpaceTerminalProfileIconTests
{
    [Fact]
    public void AnimalIconChoicesContainTwentyUniqueQuickPicks()
    {
        var choices = DevSpaceProfileSettings.ProfileIcons;

        Assert.Equal(20, choices.Count);
        Assert.Equal(20, choices.Select(x => x.Icon).Distinct().Count());
        Assert.All(choices, x =>
        {
            Assert.False(string.IsNullOrWhiteSpace(x.Icon));
            Assert.False(string.IsNullOrWhiteSpace(x.Name));
        });
    }

    [Fact]
    public void NewProfileUsesDefaultAnimalIcon()
    {
        var profile = new DevSpaceTerminalProfile { Name = "Backend" };

        Assert.Equal(DevSpaceProfileSettings.DefaultProfileIcon, profile.Icon);
        Assert.Equal($"{DevSpaceProfileSettings.DefaultProfileIcon} Backend", profile.DisplayName);
        Assert.Equal(profile.DisplayName, profile.ToString());
    }

    [Fact]
    public void ClonePreservesCustomEmoji()
    {
        var profile = new DevSpaceTerminalProfile
        {
            Name = "Backend",
            Icon = "👨‍💻",
        };

        var clone = profile.Clone(createNewId: true);

        Assert.Equal("👨‍💻", clone.Icon);
        Assert.Equal("👨‍💻 Backend", clone.DisplayName);
        Assert.NotEqual(profile.Id, clone.Id);
    }

    [Fact]
    public void ValidateProfilePreservesCustomEmoji()
    {
        var profile = new DevSpaceTerminalProfile
        {
            Name = "Backend",
            Icon = "🚀",
        };

        DevSpaceProfileSettings.ValidateProfile(profile);

        Assert.Equal("🚀", profile.Icon);
        Assert.Equal("🚀 Backend", profile.DisplayName);
    }

    [Fact]
    public void ValidateProfileFallsBackForBlankIcon()
    {
        var profile = new DevSpaceTerminalProfile
        {
            Name = "Backend",
            Icon = "   ",
        };

        DevSpaceProfileSettings.ValidateProfile(profile);

        Assert.Equal(DevSpaceProfileSettings.DefaultProfileIcon, profile.Icon);
    }

    [Fact]
    public void OldJsonWithoutIconUsesDefaultAnimalIcon()
    {
        const string json = """
            {
              "Id": "profile-1",
              "Name": "Backend",
              "Path": ".",
              "Command": "dotnet watch"
            }
            """;

        var profile = JsonSerializer.Deserialize<DevSpaceTerminalProfile>(json);

        Assert.NotNull(profile);
        Assert.Equal(DevSpaceProfileSettings.DefaultProfileIcon, profile.Icon);
        Assert.Equal($"{DevSpaceProfileSettings.DefaultProfileIcon} Backend", profile.DisplayName);
    }

    [Fact]
    public void JsonRoundTripPreservesCustomEmoji()
    {
        var profile = new DevSpaceTerminalProfile
        {
            Name = "Backend",
            Icon = "🔥",
            Path = "src/Backend",
            Command = "dotnet watch",
        };

        var json = JsonSerializer.Serialize(profile);
        var restored = JsonSerializer.Deserialize<DevSpaceTerminalProfile>(json);

        Assert.NotNull(restored);
        Assert.Equal("🔥", restored.Icon);
        Assert.Equal("🔥 Backend", restored.DisplayName);
        Assert.Equal("🔥 Backend", restored.ToString());
    }
}
