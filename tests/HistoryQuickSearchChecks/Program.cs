using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SourceGit.Commands;
using SourceGit.Models;
using SourceGit.ViewModels;
using Commit = SourceGit.Models.Commit;
using HistoryView = SourceGit.Views.Histories;

internal static class Program
{
    private static int _checks;

    private static void Main(string[] args)
    {
        var fixture = Path.Combine(Path.GetTempPath(), "sourcegit-history-search-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        SourceGit.Native.OS.GitExecutable = "git";
        SourceGit.Native.OS.BasicDirectories.ConfigDir = fixture;
        SourceGit.Native.OS.BasicDirectories.CacheDir = fixture;
        typeof(Preferences).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, new Preferences());
        AppBuilder.Configure<SourceGit.App>().UseSkia().WithInterFont()
            .ConfigureFonts(manager => manager.AddFontCollection(new Avalonia.Media.Fonts.EmbeddedFontCollection(
                new Uri("fonts:SourceGit"), new Uri("avares://SourceGit/Resources/Fonts"))))
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        try
        {
            Git(fixture, "init", "-b", "main");
            Git(fixture, "config", "user.name", "Alice Shared");
            Git(fixture, "config", "user.email", "alice@example.com");
            Git(fixture, "commit", "--allow-empty", "-m", "Shared Alpha Subject", "-m", "Body-only needle\nLiteral [brackets]. and a \"quote\"\nUnicode: \u00e1rv\u00edzt\u0171r\u0151\n" + new string('x', 9000) + "long-body needle");
            var first = Git(fixture, "rev-parse", "HEAD").Trim();
            Git(fixture, "config", "user.name", "Bob");
            Git(fixture, "config", "user.email", "bob@example.com");
            Git(fixture, "commit", "--allow-empty", "-m", "Shared second");
            var second = Git(fixture, "rev-parse", "HEAD").Trim();
            Git(fixture, "checkout", "-b", "other");
            Git(fixture, "commit", "--allow-empty", "-m", "Outside the loaded history");
            var outside = Git(fixture, "rev-parse", "HEAD").Trim();
            Git(fixture, "checkout", "main");
            var commits = new QueryCommits(fixture, "HEAD").GetResultAsync().GetAwaiter().GetResult();
            Check(Search(fixture, commits, first[..12].ToUpperInvariant()).SetEquals([first]), "SHA prefix ignores case");
            Check(Search(fixture, commits, first.Substring(8, 14)).Count == 0, "interior SHA does not match");
            Check(Search(fixture, commits, "ALICE").SetEquals([first]), "author name");
            Check(Search(fixture, commits, "BOB@EXAMPLE.COM").SetEquals([second]), "author email");
            Check(Search(fixture, commits, "alpha subject").SetEquals([first]), "subject ignores case");
            Check(Search(fixture, commits, "body-only NEEDLE").SetEquals([first]), "full multiline message");
            Check(Search(fixture, commits, "long-body needle").SetEquals([first]), "large message");
            Check(Search(fixture, commits, "[brackets].").SetEquals([first]), "literal punctuation");
            Check(Search(fixture, commits, "\u00e1rv\u00edzt\u0171r\u0151").SetEquals([first]), "Unicode");
            Check(Search(fixture, commits, "Shared").Count == 2, "OR matching across author and message");
            Check(Search(fixture, commits, outside).Count == 0, "only loaded history searched");
            Check(Search(fixture, commits, "").Count == 0, "empty query");
            Check(Search(fixture, [], "Shared").Count == 0, "empty history does not default to HEAD");
            var batched = Enumerable.Repeat(commits[0], 256).Append(commits[1]).ToList();
            Check(Search(fixture, batched, "body-only").SetEquals([first]), "matches beyond the first 256 entries");
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                try
                {
                    new QueryHistoryQuickSearch(fixture, commits, "Shared", cancelled.Token).GetResultAsync().GetAwaiter().GetResult();
                    throw new Exception("Cancelled query completed");
                }
                catch (OperationCanceledException) { Check(true, "cancelled query"); }
            }

            using (var cancellation = new CancellationTokenSource())
            {
                var manyCommits = Enumerable.Repeat(commits[0], 20000).ToList();
                var pending = new QueryHistoryQuickSearch(fixture, manyCommits, "Shared", cancellation.Token).GetResultAsync();
                cancellation.Cancel();
                try
                {
                    pending.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                    throw new Exception("Running query was not cancelled");
                }
                catch (OperationCanceledException) { Check(true, "running Git query cancels promptly"); }
            }

            var repo = new Repository(false, fixture, Path.Combine(fixture, ".git"));
            var histories = new Histories(repo) { Commits = commits, IsLoading = false };
            // Avoid unrelated detail loading; selection still drives search navigation and status.
            typeof(Histories).GetField("_ignoreSelectionChange", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(histories, true);
            histories.QuickSearchText = "Shared";
            WaitFor(() => !histories.IsQuickSearching);
            Check(histories.HasQuickSearchMatches && histories.DisplayedCommits.Count == 2, "search retains unfiltered history");
            Check(histories.SelectedCommits.Single().SHA == second, "search selects first match automatically");
            histories.SelectedCommits = [];
            histories.QuickSearchNext();
            Check(histories.SelectedCommits.Single().SHA == second, "next selects first in history order");
            histories.QuickSearchNext();
            Check(histories.SelectedCommits.Single().SHA == first, "next selects second match");
            histories.QuickSearchNext();
            Check(histories.SelectedCommits.Single().SHA == second, "next wraps");
            histories.QuickSearchPrevious();
            Check(histories.SelectedCommits.Single().SHA == first, "previous wraps");
            Check(histories.QuickSearchStatus == "2 / 2", "match counter follows selection");
            var unfilteredGraph = histories.Graph;
            histories.QuickSearchText = "body-only";
            WaitFor(() => !histories.IsQuickSearching);
            Check(ReferenceEquals(unfilteredGraph, histories.Graph), "unfiltered search does not rebuild graph");
            histories.IsQuickSearchFilterEnabled = true;
            Check(histories.DisplayedCommits.Single().SHA == first && histories.Commits.Count == 2, "filter preserves source history");
            histories.QuickSearchText = "second";
            WaitFor(() => !histories.IsQuickSearching);
            Check(histories.DisplayedCommits.Single().SHA == second, "filtered graph tolerates hidden parent");
            histories.IsQuickSearchFilterEnabled = false;
            Check(histories.DisplayedCommits.Count == 2, "filter toggle restores history");
            histories.QuickSearchText = "missing";
            histories.QuickSearchText = "Shared";
            WaitFor(() => !histories.IsQuickSearching);
            Check(histories.HasQuickSearchMatches, "new query wins over cancelled query");
            histories.Commits = [commits[0]];
            WaitFor(() => !histories.IsQuickSearching);
            Check(histories.QuickSearchStatus.EndsWith("/ 1"), "history refresh recomputes matches");
            histories.Commits = commits;
            WaitFor(() => !histories.IsQuickSearching);

            histories.SelectedCommits = [commits[1]];
            var view = new HistoryView { DataContext = histories };
            var window = new Window { Width = 1000, Height = 650, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            WaitFor(() => !histories.IsQuickSearching);
            view.FindControl<DataGrid>("CommitListContainer")!.Focus();
            window.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.Control);
            var box = view.FindControl<TextBox>("QuickSearchBox")!;
            Check(box.IsKeyboardFocusWithin, "Ctrl+F target focuses search");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Check(histories.SelectedCommits.Single().SHA == second, "Enter selects next match");
            window.KeyPressQwerty(PhysicalKey.F3, RawInputModifiers.Shift);
            Check(histories.SelectedCommits.Single().SHA == first, "Shift+F3 selects previous match");
            histories.IsQuickSearchFilterEnabled = true;
            box.Focus();
            Dispatcher.UIThread.RunJobs();
            var border = box.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BorderElement");
            Check(histories.SelectedCommits.Single().SHA == first, "filter toggle preserves visible selection");
            Check(border.Background is ISolidColorBrush brush && brush.Color == Color.Parse("#FFF2CC"), "focused filter box has yellow background");
            if (args.Length > 0)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var frame = window.CaptureRenderedFrame();
                frame?.Save(args[0]);
                Avalonia.Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var darkFrame = window.CaptureRenderedFrame();
                darkFrame?.Save(Path.Combine(Path.GetDirectoryName(args[0])!, "history-quick-search-dark.png"));
            }
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Check(histories.QuickSearchText.Length == 0 && !histories.IsQuickSearchFilterEnabled && histories.DisplayedCommits.Count == 2, "Escape clears search and restores history");
            view.FindControl<DataGrid>("CommitListContainer")!.Focus();
            window.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.Control);
            Check(box.IsKeyboardFocusWithin, "Ctrl+F works from commit list after clearing");
            histories.QuickSearchText = "no such commit";
            WaitFor(() => !histories.IsQuickSearching);
            histories.IsQuickSearchFilterEnabled = true;
            Check(histories.DisplayedCommits.Count == 0 && !histories.HasQuickSearchMatches, "no matches produces empty filtered history");
            histories.ClearQuickSearch();
            for (var i = 0; i < 60; i++)
                Git(fixture, "commit", "--allow-empty", "-m", $"Filler {i}");
            Git(fixture, "commit", "--allow-empty", "-m", "Body-only distant match");
            var longHistory = new QueryCommits(fixture, "HEAD").GetResultAsync().GetAwaiter().GetResult();
            histories.Commits = longHistory;
            histories.SelectedCommits = [];
            var grid = view.FindControl<DataGrid>("CommitListContainer")!;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.ScrollIntoView(longHistory[^1], null);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Check(!IsRowVisible(grid, longHistory[0]), "first match starts outside viewport");
            histories.QuickSearchText = "body-only";
            WaitFor(() => !histories.IsQuickSearching);
            window.UpdateLayout();
            Check(IsRowVisible(grid, longHistory[0]), "initial search scrolls first match into view");
            histories.QuickSearchNext();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Check(histories.SelectedCommits.Single() == longHistory[^1] && IsRowVisible(grid, longHistory[^1]), "next scrolls distant match into view");
            histories.QuickSearchPrevious();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Check(histories.SelectedCommits.Single() == longHistory[0] && IsRowVisible(grid, longHistory[0]), "previous scrolls distant match into view");
            histories.IsQuickSearchFilterEnabled = true;
            histories.IsQuickSearchFilterEnabled = false;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Check(IsRowVisible(grid, histories.SelectedCommits.Single()), "filter toggle keeps selected match visible");
            window.Close();
            histories.CancelQuickSearch();
            Console.WriteLine($"Passed {_checks} history quick search checks.");
        }
        finally
        {
            // Git object files are read-only on Windows. This fixture was created above.
            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            if (!Path.GetFullPath(fixture).StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(fixture).StartsWith("sourcegit-history-search-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected fixture path");
            foreach (var file in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(fixture, true);
        }
    }

    private static HashSet<string> Search(string repo, List<Commit> commits, string filter) =>
        new QueryHistoryQuickSearch(repo, commits, filter, CancellationToken.None).GetResultAsync().GetAwaiter().GetResult();

    private static bool IsRowVisible(DataGrid grid, Commit commit)
    {
        var presenter = grid.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.DataGridRowsPresenter>().First();
        return presenter.GetVisualDescendants().OfType<DataGridRow>().Any(row =>
        {
            var position = row.TranslatePoint(default, presenter);
            return row.DataContext == commit && row.IsVisible && position.HasValue &&
                position.Value.Y >= -0.5 && position.Value.Y + row.Bounds.Height <= presenter.Bounds.Height + 0.5;
        });
    }

    private static void WaitFor(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(15))
                throw new Exception("Timed out waiting for search");
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
