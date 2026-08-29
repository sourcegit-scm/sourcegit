using System.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace DevBoard.Tests;

public class DevSpacesScreenshotTests
{
    [AvaloniaFact]
    public void TerminalMain_RendersPng()
    {
        var content = new Border
        {
            Child = new TextBlock { Text = "DevSpaces terminal" }
        };

        var path = DevSpacesScreenshotRenderer.Render("terminal-main", content);
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
    }
}
