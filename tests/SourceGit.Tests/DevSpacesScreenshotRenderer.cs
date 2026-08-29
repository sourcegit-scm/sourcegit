using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Headless;

namespace SourceGit.Tests;

public static class DevSpacesScreenshotRenderer
{
    public static string Render(string scenarioId, Control content, int width = 1440, int height = 900)
    {
        var outputRoot = Environment.GetEnvironmentVariable("SOURCEGIT_SCREENSHOT_OUTPUT");
        if (string.IsNullOrWhiteSpace(outputRoot))
            outputRoot = Path.Combine(AppContext.BaseDirectory, "artifacts", "devspaces-screenshots");

        Directory.CreateDirectory(outputRoot);
        var path = Path.Combine(outputRoot, scenarioId + ".png");

        var window = new Window
        {
            Width = width,
            Height = height,
            Content = content,
            SystemDecorations = SystemDecorations.None,
        };

        window.Show();
        window.UpdateLayout();

        using var frame = window.CaptureRenderedFrame();
        if (frame is null)
            throw new InvalidOperationException($"Avalonia did not render screenshot scenario '{scenarioId}'.");

        frame.Save(path);
        window.Close();
        return path;
    }
}
