using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Fonts;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SourceGit;
using SourceGit.Commands;
using SourceGit.ViewModels;
using Views = SourceGit.Views;

internal static class Program
{
    private static int _checks;

    private static void Main(string[] args)
    {
        var fixture = Path.Combine(Path.GetTempPath(), "sourcegit-changes-message-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        SourceGit.Native.OS.GitExecutable = "git";
        SourceGit.Native.OS.BasicDirectories.ConfigDir = fixture;
        SourceGit.Native.OS.BasicDirectories.CacheDir = fixture;
        typeof(Preferences).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, new Preferences());
        AppBuilder.Configure<App>().UseSkia().WithInterFont()
            .ConfigureFonts(manager => manager.AddFontCollection(new EmbeddedFontCollection(
                new Uri("fonts:SourceGit"), new Uri("avares://SourceGit/Resources/Fonts"))))
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        Window window = null;
        CommitDetail detail = null;
        try
        {
            Git(fixture, "init", "-b", "main");
            Git(fixture, "config", "user.name", "Test Author");
            Git(fixture, "config", "user.email", "test@example.com");
            File.WriteAllText(Path.Combine(fixture, "example.txt"), "original\n");
            Git(fixture, "add", "example.txt");
            Git(fixture, "commit", "-m", "Initial file");
            var originalSHA = Git(fixture, "rev-parse", "HEAD").Trim();
            var body = string.Join("\n", Enumerable.Range(1, 24).Select(i => $"Message line {i}: full explanation of the change.")) +
                "\n\nRelated commit: " + originalSHA + "\nhttps://example.com/issues/42\nInline `code` and Unicode: \u00e1rv\u00edzt\u0171r\u0151\nFinal body line";
            File.WriteAllText(Path.Combine(fixture, "example.txt"), "updated\n");
            Git(fixture, "add", "example.txt");
            Git(fixture, "commit", "-m", "Explain the complete change", "-m", body);
            var messageSHA = Git(fixture, "rev-parse", "HEAD").Trim();
            File.WriteAllText(Path.Combine(fixture, "example.txt"), "follow-up\n");
            Git(fixture, "add", "example.txt");
            Git(fixture, "commit", "-m", "Short follow-up", "-m", "A short commit body.");
            var commits = new QueryCommits(fixture, "HEAD").GetResultAsync().GetAwaiter().GetResult();
            var longCommit = commits.Single(c => c.SHA == messageSHA);
            var shortCommit = commits[0];
            var repo = new Repository(false, fixture, Path.Combine(fixture, ".git"));
            detail = new CommitDetail(repo, new CommitDetailSharedData { ActiveTabIndex = 1 }) { Commit = longCommit };
            var view = new Views.CommitDetail { DataContext = detail };
            window = new Window { Width = 1100, Height = 650, Content = view };
            window.Show();
            WaitFor(() => detail.FullMessage?.Message.EndsWith("Final body line") == true && detail.DiffContext != null);
            Arrange(window);
            var changes = view.GetVisualDescendants().OfType<Views.CommitChanges>().Single();
            var area = changes.FindControl<Grid>("ChangesDiffArea")!;
            var messageArea = changes.FindControl<Border>("CommitMessageArea")!;
            var scroll = changes.FindControl<ScrollViewer>("CommitMessageScrollViewer")!;
            var message = changes.FindControl<Views.CommitMessagePresenter>("ChangesCommitMessage")!;
            var preview = changes.FindControl<SelectableTextBlock>("CollapsedCommitMessage")!;
            var toggle = changes.FindControl<ToggleButton>("CommitMessageToggle")!;
            var splitter = changes.FindControl<GridSplitter>("CommitMessageSplitter")!;
            var diff = changes.FindControl<ContentControl>("ChangesDiffContent")!;
            var files = changes.GetVisualDescendants().OfType<Views.ChangeCollectionView>().Single();
            var layout = Preferences.Instance.Layout;
            var initialHeight = messageArea.Bounds.Height;
            Check(layout.IsCommitDetailChangesMessageExpanded && Near(initialHeight, 96), "message pane starts expanded with a few lines reserved");
            Check(message.Inlines!.Text == detail.FullMessage.Message && message.Inlines.Text.Contains("Message line 24") &&
                message.Inlines.Text.Contains("Final body line"), "all message lines are available without truncation");
            Check(ReferenceEquals(message.FullMessage, detail.FullMessage) && detail.FullMessage.Inlines.Count >= 3,
                "message reuses parsed links, commit references, and inline formatting");
            Check(scroll.Extent.Height > scroll.Viewport.Height && scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto,
                "long messages scroll inside the reserved area");
            Check(diff.Bounds.Top >= messageArea.Bounds.Bottom + area.RowDefinitions[1].ActualHeight - 1 && diff.Bounds.Height > 80,
                "diff remains below message and splitter");
            Check(files.Bounds.Height > messageArea.Bounds.Height * 3, "changed-file list keeps its full height");
            var filesHeight = files.Bounds.Height;
            Click(window, toggle);
            Arrange(window);
            Check(!layout.IsCommitDetailChangesMessageExpanded && messageArea.Bounds.Height <= 30 && preview.IsVisible && preview.Text == longCommit.Subject,
                "collapse shows only a single subject line");
            Check(!scroll.IsVisible && !splitter.IsEnabled, "collapsed message does not leave a scrollable body or active resize handle");
            Click(window, toggle);
            Arrange(window);
            Check(Near(messageArea.Bounds.Height, initialHeight), "expand restores previous height");
            var start = splitter.TranslatePoint(new Point(splitter.Bounds.Width / 2, splitter.Bounds.Height / 2), window)!.Value;
            window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(start + new Vector(0, 40), RawInputModifiers.LeftMouseButton);
            window.MouseUp(start + new Vector(0, 40), MouseButton.Left, RawInputModifiers.None);
            Arrange(window);
            var resizedHeight = messageArea.Bounds.Height;
            Check(resizedHeight > initialHeight + 20 && Near(resizedHeight, layout.CommitDetailChangesMessageHeight.Value),
                "dragging horizontal splitter resizes and remembers message height");
            Check(Near(files.Bounds.Height, filesHeight), "resizing message leaves changed-file list height unchanged");
            Click(window, toggle);
            Arrange(window);
            var layoutOptions = new JsonSerializerOptions();
            layoutOptions.Converters.Add(new GridLengthConverter());
            var saved = JsonSerializer.Serialize(layout, layoutOptions);
            var restored = JsonSerializer.Deserialize<LayoutInfo>(saved, layoutOptions)!;
            Check(!restored.IsCommitDetailChangesMessageExpanded && Near(restored.CommitDetailChangesMessageHeight.Value, resizedHeight),
                "saved preferences preserve collapsed state and expanded height independently");
            Click(window, toggle);
            Arrange(window);
            Check(Near(messageArea.Bounds.Height, resizedHeight), "expanding after resize restores resized height");
            scroll.Offset = new Vector(0, scroll.Extent.Height);
            Arrange(window);
            Check(scroll.Offset.Y > 0, "message can scroll to its last lines");
            detail.Commit = shortCommit;
            WaitFor(() => detail.FullMessage?.Message.Contains("A short commit body.") == true);
            Arrange(window);
            Check(message.Inlines!.Text.Contains("A short commit body.") && !message.Inlines.Text.Contains("Message line 24"),
                "changing selected commit updates the displayed full message");
            Check(scroll.Offset.Y == 0 && Near(messageArea.Bounds.Height, resizedHeight), "commit changes reset message scroll and retain chosen size");
            Click(window, toggle);
            detail.Commit = longCommit;
            WaitFor(() => detail.FullMessage?.Message.EndsWith("Final body line") == true);
            Arrange(window);
            Check(preview.Text == longCommit.Subject && messageArea.Bounds.Height <= 30, "collapsed subject follows selected commit");
            Click(window, toggle);
            detail.ActiveTabIndex = 0;
            Arrange(window);
            detail.ActiveTabIndex = 1;
            Arrange(window);
            Check(layout.IsCommitDetailChangesMessageExpanded && Near(messageArea.Bounds.Height, resizedHeight), "switching detail tabs retains pane size");
            window.Height = 220;
            Arrange(window);
            Check(messageArea.Bounds.Height <= Math.Max(28, area.Bounds.Height - 84) + 1 && diff.Bounds.Height >= 79,
                "small layouts leave room for the diff");
            window.Height = 650;
            Arrange(window);
            Check(Near(messageArea.Bounds.Height, resizedHeight), "larger layout restores preferred message size");
            if (args.Length > 0)
            {
                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                {
                    Application.Current!.RequestedThemeVariant = theme;
                    Arrange(window);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    using var frame = window.CaptureRenderedFrame();
                    frame!.Save(Path.Combine(args[0], $"changes-message-{theme.ToString().ToLowerInvariant()}.png"));
                }
                Click(window, toggle);
                Arrange(window);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var collapsed = window.CaptureRenderedFrame();
                collapsed!.Save(Path.Combine(args[0], "changes-message-collapsed.png"));
            }
            detail.Commit = shortCommit;
            detail.Commit = longCommit;
            detail.Commit = shortCommit;
            WaitFor(() => detail.FullMessage?.Message.Contains("A short commit body.") == true);
            Arrange(window);
            Check(!message.Inlines!.Text.Contains("Final body line"), "rapid commit selection does not retain an earlier message");
            detail.Commit = longCommit;
            detail.Commit = null;
            WaitFor(() => detail.FullMessage == null);
            Arrange(window);
            Check(message.Inlines!.Count == 0, "clearing commit clears the message pane");
            Console.WriteLine($"Passed {_checks} Changes message checks.");
        }
        finally
        {
            window?.Close();
            if (detail != null)
                detail.Commit = null;
            if (!Path.GetFullPath(fixture).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unexpected fixture path");
            for (var attempt = 0; attempt < 30; attempt++)
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
                        File.SetAttributes(file, FileAttributes.Normal);
                    Directory.Delete(fixture, true);
                    break;
                }
                catch (IOException) when (attempt < 29) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(100); }
                catch (UnauthorizedAccessException) when (attempt < 29) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(100); }
            }
        }
    }

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Arrange(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static bool Near(double left, double right) => Math.Abs(left - right) < 1;

    private static void WaitFor(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(15))
                throw new Exception("Timed out waiting for commit details");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Check(bool condition, string label)
    {
        if (!condition)
            throw new Exception(label);
        _checks++;
        Console.WriteLine("PASS " + label);
    }

    private static string Git(string cwd, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new Exception(error);
        return output;
    }
}
