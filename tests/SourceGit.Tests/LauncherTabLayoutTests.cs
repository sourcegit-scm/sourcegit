using SourceGit.Models;
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
}
