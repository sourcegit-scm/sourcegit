using System;
using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;

internal static class Program
{
    public static string HelperPath { get; private set; } = string.Empty;

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length != 1)
            throw new ArgumentException("Pass the absolute path to WindowsTerminalHostProbe.Helper.exe as the only argument.");

        HelperPath = Path.GetFullPath(args[0]);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime([]);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<ProbeApp>()
            .UsePlatformDetect()
            .LogToTrace();
}

internal sealed class ProbeApp : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new ProbeWindow(Program.HelperPath);

        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed class ProbeWindow : Window
{
    public ProbeWindow(string helperPath)
    {
        Title = "SourceGit Windows Terminal Host Probe";
        Width = 1000;
        Height = 700;

        var host = new ProbeNativeHost(helperPath);
        var toggle = new Button
        {
            Content = "Hide Terminal",
            Margin = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        toggle.Click += (_, _) =>
        {
            host.IsVisible = !host.IsVisible;
            toggle.Content = host.IsVisible ? "Hide Terminal" : "Show Terminal";
        };

        var hiddenMarker = new Border
        {
            Background = Brushes.DarkSlateBlue,
            Child = new TextBlock
            {
                Text = "NATIVE TERMINAL HIDDEN — this area must be fully visible with no HWND bleed-through.",
                Foreground = Brushes.White,
                Margin = new Thickness(24),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
        };
        grid.Children.Add(toggle);
        Grid.SetRow(hiddenMarker, 1);
        grid.Children.Add(hiddenMarker);
        Grid.SetRow(host, 1);
        grid.Children.Add(host);
        Content = grid;
    }
}
