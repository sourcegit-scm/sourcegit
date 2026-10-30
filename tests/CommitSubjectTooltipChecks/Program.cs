using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using SourceGit.Views;

AppBuilder.Configure<Application>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();
Application.Current.Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());

var subject = new CommitSubjectPresenter
{
    Subject = "Fix #123",
    FullMessage = "Fix #123\n\nThe complete commit message body.",
    FontFamily = FontFamily.Default,
    CodeFontFamily = FontFamily.Default,
    FontSize = 14,
    Width = 120,
    Height = 26,
    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
    IssueTrackers = new AvaloniaList<SourceGit.Models.IssueTracker>
    {
        new() { RegexString = @"#(\d+)", URLTemplate = "https://example.com/issues/$1" }
    }
};
var cell = new DataGridCell
{
    Content = subject,
    Template = new FuncControlTemplate<DataGridCell>((_, _) => new Border
    {
        Background = Brushes.Transparent,
        Child = new Avalonia.Controls.Presenters.ContentPresenter { Name = "PART_ContentPresenter", Content = subject }
    })
};
var row = new Grid { ColumnDefinitions = new ColumnDefinitions("600,100"), Height = 30 };
row.Children.Add(cell);
var neighbor = new Border { Background = Brushes.Transparent };
Grid.SetColumn(neighbor, 1);
row.Children.Add(neighbor);
var window = new Window { Width = 700, Height = 100, Content = row };
window.Show();
Pump(100);
AvaloniaHeadlessPlatform.ForceRenderTimerTick();
var y = row.Bounds.Y + 15;
window.MouseMove(new Point(650, y));
Pump(50);
window.MouseMove(new Point(580, y));
Pump(300);
Check(!ToolTip.GetIsOpen(cell), "short hover over blank cell space does not open tooltip");
Pump(900);
Check(ToolTip.GetIsOpen(cell), "blank space at the far end of SUBJECT opens tooltip");
Check(ToolTip.GetTip(cell) is TextBlock { Text: var message } && message == subject.FullMessage,
    "cell tooltip contains the full multiline commit message");

window.MouseMove(new Point(32, y));
Pump(1200);
Check(ToolTip.GetIsOpen(subject), "issue-link URL tooltip opens over the link");
Check(ToolTip.GetTip(subject) is string link && link == "https://example.com/issues/123",
    "issue links retain their URL tooltip");
window.MouseMove(new Point(580, y));
Pump(1200);
Check(ToolTip.GetTip(subject) == null && ToolTip.GetIsOpen(cell),
    "moving from an issue link back to blank space restores the message tooltip");

window.MouseMove(new Point(650, y));
Pump(100);
Check(!ToolTip.GetIsOpen(cell), "moving into another column closes the message tooltip");
window.MouseMove(new Point(580, y));
Pump(1200);
subject.DataContext = new object();
subject.Subject = "Another commit";
subject.FullMessage = "Another commit\n\nAnother body";
Check(!ToolTip.GetIsOpen(cell), "recycling a row closes the cell tooltip");
Check(((TextBlock)ToolTip.GetTip(cell)).Text == subject.FullMessage, "recycled row replaces previous message");

row.Children.Remove(cell);
Check(ToolTip.GetTip(cell) == null, "detaching the presenter clears the cell tooltip");
window.Close();

static void Check(bool value, string description)
{
    if (!value)
        throw new InvalidOperationException(description);
    Console.WriteLine($"PASS: {description}");
}

static void Pump(int milliseconds)
{
    var frame = new DispatcherFrame();
    DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(milliseconds));
    Dispatcher.UIThread.PushFrame(frame);
}
