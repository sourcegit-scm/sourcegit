using SourceGit.Models;

namespace SourceGit.Tests;

public class LauncherCloseControllerTests
{
    [Fact]
    public void First_close_request_requires_confirmation()
    {
        var controller = new LauncherCloseController();

        Assert.Equal(LauncherCloseAction.Confirm, controller.OnCloseRequested());
    }

    [Fact]
    public void Yes_allows_the_next_close_to_exit()
    {
        var controller = new LauncherCloseController();

        Assert.Equal(LauncherCloseAction.Exit, controller.Apply(CloseAppDecision.Yes));
        Assert.Equal(LauncherCloseAction.Exit, controller.OnCloseRequested());
    }

    [Fact]
    public void No_keeps_the_launcher_open()
    {
        var controller = new LauncherCloseController();

        Assert.Equal(LauncherCloseAction.KeepOpen, controller.Apply(CloseAppDecision.No));
        Assert.Equal(LauncherCloseAction.Confirm, controller.OnCloseRequested());
    }

    [Fact]
    public void Add_to_tray_hides_without_enabling_exit()
    {
        var controller = new LauncherCloseController();

        Assert.Equal(LauncherCloseAction.HideToTray, controller.Apply(CloseAppDecision.AddToTray));
        Assert.Equal(LauncherCloseAction.Confirm, controller.OnCloseRequested());
    }
}
