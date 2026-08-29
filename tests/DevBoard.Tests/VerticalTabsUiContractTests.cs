using DevBoard.Views;
using Xunit;

namespace DevBoard.Tests;

public sealed class VerticalTabsUiContractTests
{
    [Fact]
    public void VerticalTabsUiTypesExposeRequiredContracts()
    {
        var assembly = typeof(LauncherTabBar).Assembly;
        var preferencesView = assembly.GetType("DevBoard.Views.DevSpacesPreferences");
        var isVertical = typeof(LauncherTabBar).GetProperty("IsVertical");

        Assert.NotNull(preferencesView);
        Assert.NotNull(isVertical);
        Assert.Equal(typeof(bool), isVertical.PropertyType);
    }
}
