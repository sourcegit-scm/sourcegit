using SourceGit.Views;
using Xunit;

namespace SourceGit.Tests;

public sealed class VerticalTabsUiContractTests
{
    [Fact]
    public void VerticalTabsUiTypesExposeRequiredContracts()
    {
        var assembly = typeof(LauncherTabBar).Assembly;
        var preferencesView = assembly.GetType("SourceGit.Views.DevSpacesPreferences");
        var isVertical = typeof(LauncherTabBar).GetProperty("IsVertical");

        Assert.NotNull(preferencesView);
        Assert.NotNull(isVertical);
        Assert.Equal(typeof(bool), isVertical.PropertyType);
    }
}
