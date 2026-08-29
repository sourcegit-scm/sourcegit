using System.Text.Json;

using SourceGit.Models;
using SourceGit.ViewModels;
using Xunit;

namespace SourceGit.Tests;

public sealed class LauncherTabLayoutTests
{
    [Fact]
    public void DefaultsToHorizontalWithExpectedVerticalWidth()
    {
        var settings = new LauncherTabSettings();

        Assert.Equal(LauncherTabLayout.Horizontal, settings.Layout);
        Assert.Equal(220, settings.VerticalWidth);
    }

    [Theory]
    [InlineData(100, 160)]
    [InlineData(160, 160)]
    [InlineData(220, 220)]
    [InlineData(420, 420)]
    [InlineData(500, 420)]
    public void VerticalWidthIsClamped(double requested, double expected)
    {
        var settings = new LauncherTabSettings { VerticalWidth = requested };

        Assert.Equal(expected, settings.VerticalWidth);
    }

    [Fact]
    public void LayoutChangeRaisesPropertyChangedForLayoutAndIsVertical()
    {
        var settings = new LauncherTabSettings();
        var changed = new List<string>();
        settings.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        settings.Layout = LauncherTabLayout.Vertical;

        Assert.Contains(nameof(LauncherTabSettings.Layout), changed);
        Assert.Contains(nameof(LauncherTabSettings.IsVertical), changed);
        Assert.True(settings.IsVertical);
    }

    [Fact]
    public void PreferencesOwnLauncherTabSettings()
    {
        var preferences = new Preferences();

        Assert.Equal(LauncherTabLayout.Horizontal, preferences.LauncherTabs.Layout);
        Assert.Equal(220, preferences.LauncherTabs.VerticalWidth);
    }

    [Fact]
    public void LauncherTabSettingsRoundTripThroughJson()
    {
        var settings = new LauncherTabSettings
        {
            Layout = LauncherTabLayout.Vertical,
            VerticalWidth = 315,
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<LauncherTabSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(LauncherTabLayout.Vertical, restored.Layout);
        Assert.Equal(315, restored.VerticalWidth);
    }
}
