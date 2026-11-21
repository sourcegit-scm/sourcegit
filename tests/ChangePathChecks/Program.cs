using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SourceGit.Views;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        SourceGit.App.BuildAvaloniaApp().SetupWithoutStarting();
        var text = new ChangePathTextBlock { Foreground = Brushes.White, Path = "very/long/directory/structure/filename.cs" };
        Measure(text, 1000);
        Check(text.Text == text.Path, "Full path fits");
        text.DirectoryForeground = Brushes.DarkGray;
        Measure(text, 1000);
        Measure(text, 180);
        Check(text.Text.StartsWith("…") && text.Text.EndsWith("filename.cs"), "Leading ellipsis preserves filename");
        Check(text.DesiredSize.Width <= 180, "Trimmed path fits");
        Measure(text, 1);
        Check(text.Text == "…filename.cs" && text.MinWidth > 1, "Narrow viewport preserves whole filename");
        Measure(text, 1000);
        Check(text.Text == text.Path, "Widening restores path");
        text.Path = "filename-without-directory.cs";
        Measure(text, 1);
        Check(text.Text == text.Path, "Root filename never trimmed");
        text.Path = "a/😀e\u0301/very/long/filename.cs";
        Measure(text, 160);
        Check(text.Text.EndsWith("filename.cs") && !text.Text.Contains('\uFFFD'), "Unicode filename preserved");
        text.Path = "";
        Measure(text, 1);
        Check(string.IsNullOrEmpty(text.Text), "Empty path");

        var row = new ChangeListRow { ColumnDefinitions = ColumnDefinitions.Parse("Auto,Auto,*"), ViewportWidth = 180 };
        var icon = new Border { Width = 18 };
        text.Path = "very/long/directory/structure/filename.cs";
        Grid.SetColumn(text, 2);
        row.Children.Add(icon);
        row.Children.Add(text);
        row.Measure(Size.Infinity);
        Check(text.Text.StartsWith("…") && text.DesiredSize.Width <= 162, "Row constrains path to viewport");
        row.ViewportWidth = 25;
        row.Measure(Size.Infinity);
        Check(text.Text == "…filename.cs" && row.DesiredSize.Width > 25, "Long filename keeps horizontal scroll extent");
        SourceGit.ViewModels.Preferences.Instance.SetCanModify();
        SourceGit.ViewModels.Preferences.Instance.TrimLeadingPathsInChanges = true;
        foreach (var unstaged in new[] { true, false })
        {
            var view = new ChangeCollectionView
            {
                IsUnstagedChange = unstaged,
                ViewMode = SourceGit.Models.ChangeViewMode.List,
                Changes = [new SourceGit.Models.Change { Path = "very/long/directory/structure/filename.cs" }],
            };
            var window = new Window { Width = 180, Height = 150, Content = view };
            window.Show();
            for (var i = 0; i < 10; i++)
                Dispatcher.UIThread.RunJobs();
            var label = view.GetVisualDescendants().OfType<ChangePathTextBlock>().Single();
            Check(label.Text.StartsWith("…") && label.Text.EndsWith("filename.cs"), "Actual staged/unstaged template truncates correctly");
            CheckPathColors(label);
            var preferences = SourceGit.ViewModels.Preferences.Instance;
            preferences.SetCanModify();
            preferences.TrimLeadingPathsInChanges = false;
            for (var i = 0; i < 10; i++)
                Dispatcher.UIThread.RunJobs();
            Check(label.Text == label.Path, "Preference disables trimming immediately");
            CheckPathColors(label);
            preferences.TrimLeadingPathsInChanges = true;
            for (var i = 0; i < 10; i++)
                Dispatcher.UIThread.RunJobs();
            Check(label.Text.StartsWith("…"), "Preference enables trimming immediately");
            window.Width = 800;
            for (var i = 0; i < 10; i++)
                Dispatcher.UIThread.RunJobs();
            Check(label.Text == label.Path, "Actual template responds to resize");
            window.Close();
        }
        Console.WriteLine("PASS: 18 changed-path layout checks and directory/filename color checks");
    }

    private static void Measure(Control control, double width)
    {
        control.InvalidateMeasure();
        control.Measure(new Size(width, 24));
        if (control is ChangePathTextBlock path)
            CheckPathColors(path);
    }

    private static void CheckPathColors(ChangePathTextBlock label)
    {
        var filenameLength = label.Path.Length - (label.Path.LastIndexOf('/') + 1);
        var directoryLength = label.Text.Length - filenameLength;
        var position = 0;
        foreach (var run in label.TextLayout.TextLines.SelectMany(line => line.TextRuns))
        {
            if (position >= label.Text.Length)
                break;
            var expected = position < directoryLength ? label.DirectoryForeground : label.Foreground;
            Check(Equals(run.Properties.ForegroundBrush, expected), "Directory is gray and filename keeps normal foreground");
            Check(position >= directoryLength || position + run.Length <= directoryLength, "Color boundary preserves full filename");
            position += run.Length;
        }
        Check(position >= label.Text.Length, "Color checks cover displayed text");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
