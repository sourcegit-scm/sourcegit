using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(SourceGit.Tests.DevSpacesScreenshotAppBuilder))]

namespace SourceGit.Tests;

public sealed class DevSpacesScreenshotApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}

public static class DevSpacesScreenshotAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<DevSpacesScreenshotApplication>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions
        {
            UseHeadlessDrawing = false,
        });
}
