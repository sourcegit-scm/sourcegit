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
            if (args.Length >= 2 && args[0] == "--repository")
            {
                CheckRepositoryGraph(args[1], args.Length > 2 ? args[2] : null);
                return;
            }
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
            var sidebarSearch = new TextBox { Watermark = "Sidebar search" };
            var shell = new Grid { RowDefinitions = RowDefinitions.Parse("28,*") };
            shell.Children.Add(sidebarSearch);
            Grid.SetRow(view, 1);
            shell.Children.Add(view);
            var window = new Window { Width = 1000, Height = 650, Content = shell };
            window.Show();
            SetWindowActive(window, true);
            Dispatcher.UIThread.RunJobs();
            WaitFor(() => !histories.IsQuickSearching);
            view.FindControl<DataGrid>("CommitListContainer")!.Focus();
            window.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.Control);
            var box = view.FindControl<TextBox>("QuickSearchBox")!;
            Check(box.IsKeyboardFocusWithin, "Ctrl+F target focuses search");
            view.FindControl<DataGrid>("CommitListContainer")!.Focus();
            window.KeyPressQwerty(PhysicalKey.F3, RawInputModifiers.None);
            Check(histories.SelectedCommits.Single().SHA == second, "F3 selects next match from the commit list");
            Check(!box.IsKeyboardFocusWithin, "F3 keeps focus in the commit list");
            window.KeyPressQwerty(PhysicalKey.F3, RawInputModifiers.Shift);
            Check(histories.SelectedCommits.Single().SHA == first, "Shift+F3 selects previous match from the commit list");
            window.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.Control);
            Check(box.IsKeyboardFocusWithin && Math.Abs(box.SelectionEnd - box.SelectionStart) == box.Text.Length,
                "Ctrl+F focuses quick search and selects its text");
            window.KeyTextInput("body-only");
            Check(histories.QuickSearchText == "body-only", "typing immediately after Ctrl+F replaces the search query");
            WaitFor(() => !histories.IsQuickSearching);
            sidebarSearch.Focus();
            window.KeyPressQwerty(PhysicalKey.F3, RawInputModifiers.None);
            Check(sidebarSearch.IsKeyboardFocusWithin && histories.SelectedCommits.Single().SHA == first,
                "F3 in the sidebar does not activate history search");
            view.IsVisible = false;
            sidebarSearch.Focus();
            window.KeyPressQwerty(PhysicalKey.F3, RawInputModifiers.None);
            Check(sidebarSearch.IsKeyboardFocusWithin, "hidden history does not intercept F3");
            view.IsVisible = true;
            box.Focus();
            histories.QuickSearchText = "Shared";
            WaitFor(() => !histories.IsQuickSearching);
            window.KeyPressQwerty(PhysicalKey.F3, RawInputModifiers.None);
            Check(histories.SelectedCommits.Single().SHA == second && box.IsKeyboardFocusWithin,
                "F3 selects next match while retaining search-box focus");
            window.KeyPressQwerty(PhysicalKey.F3, RawInputModifiers.Shift);
            Check(histories.SelectedCommits.Single().SHA == first, "Shift+F3 selects previous match in search box");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Check(histories.SelectedCommits.Single().SHA == second, "Enter selects next match");
            window.KeyPressQwerty(PhysicalKey.F3, RawInputModifiers.Shift);
            Check(histories.SelectedCommits.Single().SHA == first, "Shift+F3 selects previous match");
            histories.IsQuickSearchFilterEnabled = true;
            box.Focus();
            Dispatcher.UIThread.RunJobs();
            var border = box.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BorderElement");
            Check(histories.SelectedCommits.Single().SHA == first, "filter toggle preserves visible selection");
            foreach (var theme in new[] { Avalonia.Styling.ThemeVariant.Light, Avalonia.Styling.ThemeVariant.Dark })
            {
                Application.Current!.RequestedThemeVariant = theme;
                Dispatcher.UIThread.RunJobs();
                box.Focus();
                var textPresenter = box.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>()
                    .Single(p => p.Name == "PART_TextPresenter");
                SetWindowActive(window, false);
                Dispatcher.UIThread.RunJobs();
                Check(!window.IsActive && textPresenter.CaretBrush is ISolidColorBrush inactiveCaret && inactiveCaret.Color.A == 0,
                    $"{theme}: inactive window hides the search caret");
                Check(border.BorderBrush is ISolidColorBrush inactiveBorder && box.BorderBrush is ISolidColorBrush normalBorder &&
                    inactiveBorder.Color == normalBorder.Color, $"{theme}: inactive search uses its normal border instead of active focus styling");
                Check(box.SelectionBrush is ISolidColorBrush selectionBrush && box.BorderBrush is ISolidColorBrush borderBrush &&
                    selectionBrush.Color == borderBrush.Color, $"{theme}: inactive text selection uses a neutral highlight");
                if (args.Length > 0)
                {
                    window.UpdateLayout();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    using var inactiveFrame = window.CaptureRenderedFrame();
                    inactiveFrame!.Save(Path.Combine(Path.GetDirectoryName(args[0])!, $"history-inactive-{theme.ToString().ToLowerInvariant()}.png"));
                }
                var otherInput = new TextBox();
                var otherWindow = new Window { Width = 200, Height = 80, Content = otherInput };
                otherWindow.Show();
                SetWindowActive(otherWindow, true);
                otherInput.Focus();
                var previousQuery = histories.QuickSearchText;
                otherWindow.KeyPressQwerty(PhysicalKey.F3, RawInputModifiers.None);
                otherWindow.KeyTextInput("another window");
                Check(histories.QuickSearchText == previousQuery && otherInput.Text == "another window" &&
                    textPresenter.CaretBrush is ISolidColorBrush hiddenCaret && hiddenCaret.Color.A == 0,
                    $"{theme}: keys in another window do not activate or edit history search");
                otherWindow.Close();
                SetWindowActive(window, true);
                box.Focus();
                Dispatcher.UIThread.RunJobs();
                Check((textPresenter.CaretBrush ?? textPresenter.Foreground) is ISolidColorBrush activeCaret && activeCaret.Color.A > 0,
                    $"{theme}: reactivating the window restores the caret");
                var list = view.FindControl<DataGrid>("CommitListContainer")!;
                var tinted = theme == Avalonia.Styling.ThemeVariant.Light ? Color.Parse("#FFFFF9E6") : Color.Parse("#FF302D20");
                var normal = theme == Avalonia.Styling.ThemeVariant.Light ? Color.Parse("#FFFFFFFF") : Color.Parse("#FF1C1C1C");
                var presenter = list.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.DataGridRowsPresenter>().Single();
                Check(list.Background is ISolidColorBrush background && background.Color == tinted &&
                    presenter.Background is ISolidColorBrush rows && rows.Color == tinted, $"{theme}: active filter tints history rows");
                var headers = list.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.DataGridColumnHeadersPresenter>().Single();
                var headerColor = theme == Avalonia.Styling.ThemeVariant.Light ? Color.Parse("#FFFBFBFB") : Color.Parse("#FF272727");
                Check(headers.Background is ISolidColorBrush headerBrush && headerBrush.Color == headerColor,
                    $"{theme}: column headers retain normal background during filtering");
                Check(border.Background is not ISolidColorBrush input || input.Color != tinted && input.Color != Color.Parse("#FFF2CC"),
                    $"{theme}: search field keeps normal theme background");
                var graph = view.FindControl<SourceGit.Views.CommitGraph>("CommitGraph")!;
                Check(graph.DotBrush is ISolidColorBrush dots && dots.Color == tinted, $"{theme}: graph dots use history background");
                histories.IsQuickSearchFilterEnabled = false;
                Dispatcher.UIThread.RunJobs();
                Check(list.Background is ISolidColorBrush unfiltered && unfiltered.Color == normal, $"{theme}: query without filter has no tint");
                histories.IsQuickSearchFilterEnabled = true;
                histories.QuickSearchText = string.Empty;
                Dispatcher.UIThread.RunJobs();
                Check(!histories.IsQuickSearchFilterActive && list.Background is ISolidColorBrush empty && empty.Color == normal,
                    $"{theme}: enabled filter with empty query has no tint");
                histories.QuickSearchText = "body-only";
                WaitFor(() => !histories.IsQuickSearching);
                Check(histories.IsQuickSearchFilterActive && list.Background is ISolidColorBrush restored && restored.Color == tinted,
                    $"{theme}: editing nonempty query restores active tint");
                var clearButton = (Button)box.InnerRightContent!;
                var clearIcon = (Avalonia.Controls.Shapes.Path)clearButton.Content!;
                Check(clearButton.IsVisible && clearIcon.Fill is ISolidColorBrush iconBrush && iconBrush.Color.A > 0,
                    $"{theme}: nonempty search shows a visible clear icon");
                window.UpdateLayout();
                var clearPoint = clearButton.TranslatePoint(new Point(clearButton.Bounds.Width / 2, clearButton.Bounds.Height / 2), window)!.Value;
                window.MouseDown(clearPoint, MouseButton.Left, RawInputModifiers.None);
                window.MouseUp(clearPoint, MouseButton.Left, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Check(histories.QuickSearchText.Length == 0 && box.Text.Length == 0 && histories.DisplayedCommits.Count == 2,
                    $"{theme}: one click clears search and restores history");
                Check(!clearButton.IsVisible && box.IsKeyboardFocusWithin, $"{theme}: empty search hides clear icon and retains focus");
                histories.QuickSearchText = "body-only";
                histories.IsQuickSearchFilterEnabled = true;
                WaitFor(() => !histories.IsQuickSearching);
            }
            Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
            Dispatcher.UIThread.RunJobs();
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
            histories.QuickSearchText = "Shared";
            WaitFor(() => !histories.IsQuickSearching);
            Check(histories.SelectedCommits.Count == 1, "matching query selects a row before no-match edit");
            histories.QuickSearchText = "no such commit";
            WaitFor(() => !histories.IsQuickSearching);
            Check(histories.SelectedCommits.Count == 0 && view.FindControl<DataGrid>("CommitListContainer")!.SelectedItems.Count == 0 &&
                histories.DisplayedCommits.Count == 2, "no-match edit clears selection without filtering");
            histories.IsQuickSearchFilterEnabled = true;
            Check(histories.DisplayedCommits.Count == 0 && !histories.HasQuickSearchMatches, "no matches produces empty filtered history");
            histories.QuickSearchText = "Shared";
            WaitFor(() => !histories.IsQuickSearching);
            Check(histories.SelectedCommits.Count == 1, "matching query restores selection while filtered");
            histories.QuickSearchText = "no such filtered commit";
            WaitFor(() => !histories.IsQuickSearching);
            Check(histories.SelectedCommits.Count == 0 && view.FindControl<DataGrid>("CommitListContainer")!.SelectedItems.Count == 0,
                "no-match edit clears selection while filtered");
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
            Check(histories.Graph.Dots.Count == 2 && histories.Graph.Dots.All(dot => dot.Center.X == 10), "filtered linear history stays in one lane across hidden commits");
            Check(histories.Graph.Paths.Count == 1, "filtered matches share their ancestral path");
            histories.IsQuickSearchFilterEnabled = false;
            Check(longHistory[0].Parents.Single() == longHistory[1].SHA, "filtering preserves original parent hashes");
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Check(IsRowVisible(grid, histories.SelectedCommits.Single()), "filter toggle keeps selected match visible");
            CheckFilteredGraphTopology();
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

    private static void CheckRepositoryGraph(string path, string preview)
    {
        var commits = new QueryCommits(path, "--branches --remotes --tags HEAD --date-order -20000").GetResultAsync().GetAwaiter().GetResult();
        var matches = Search(path, commits, "fix:");
        var visible = commits.FindAll(c => matches.Contains(c.SHA));
        var full = CommitGraph.Generate(commits, false, CommitGraphHighlighting.All, []);
        var filtered = CommitGraph.GenerateFiltered(commits, visible, false, CommitGraphHighlighting.All, []);
        var fullWidth = full.Dots.Max(dot => dot.Center.X);
        var filteredWidth = filtered.Dots.Max(dot => dot.Center.X);
        Console.WriteLine($"Repository: {commits.Count} commits, {visible.Count} matches; full width {fullWidth}, filtered width {filteredWidth}");
        if (preview != null)
        {
            var repo = new Repository(false, path, Path.Combine(path, ".git"));
            var histories = new Histories(repo) { Commits = commits, IsLoading = false, IsCollapseDetails = true };
            typeof(Histories).GetField("_ignoreSelectionChange", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(histories, true);
            var view = new HistoryView { DataContext = histories };
            var window = new Window { Width = 1600, Height = 1000, Content = view };
            Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            window.Show();
            histories.QuickSearchText = "fix:";
            histories.IsQuickSearchFilterEnabled = true;
            WaitFor(() => !histories.IsQuickSearching);
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var frame = window.CaptureRenderedFrame();
            frame!.Save(preview);
            histories.SelectedCommits = [histories.DisplayedCommits[^1]];
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var bottom = window.CaptureRenderedFrame();
            bottom!.Save(Path.Combine(Path.GetDirectoryName(preview)!, Path.GetFileNameWithoutExtension(preview) + "-bottom.png"));
            window.Close();
            histories.CancelQuickSearch();
        }
        Check(filteredWidth <= fullWidth + 12, "real repository filtered graph does not grow phantom lanes");
    }

    private static void CheckFilteredGraphTopology()
    {
        var root = new Commit { SHA = "root" };
        var left = new Commit { SHA = "left", Parents = [root.SHA] };
        var right = new Commit { SHA = "right", Parents = [root.SHA] };
        var hiddenMerge = new Commit { SHA = "hidden-merge", Parents = [left.SHA, right.SHA] };
        var tip = new Commit { SHA = "tip", Parents = [hiddenMerge.SHA] };
        var all = new List<Commit> { tip, hiddenMerge, left, right, root };
        var graph = CommitGraph.GenerateFiltered(all, [tip, left, right, root], false, CommitGraphHighlighting.All, []);
        Check(graph.Dots.Count == 4 && graph.Paths.Count == 2, "hidden merge retains both visible parent paths");
        Check(graph.Dots.Max(dot => dot.Center.X) <= 22, "hidden merge does not create extra lanes");
        graph = CommitGraph.GenerateFiltered(all, [tip, right], true, CommitGraphHighlighting.All, []);
        Check(graph.Paths.Count == 0, "first-parent mode does not connect through a hidden second parent");
        graph = CommitGraph.GenerateFiltered(all, [tip], false, CommitGraphHighlighting.All, []);
        Check(graph.Paths.Count == 0 && graph.Dots.Single().Center.X == 10, "no dangling paths for ancestors without visible matches");
        Check(hiddenMerge.Parents.SequenceEqual([left.SHA, right.SHA]), "projecting hidden merges preserves original ancestry");
        var chain = Enumerable.Range(0, 200).Select(i => new Commit { SHA = $"visible-{i}" }).ToList();
        for (var i = 0; i < chain.Count - 1; i++)
            chain[i].Parents = [chain[i + 1].SHA];
        var hidden = Enumerable.Range(0, chain.Count).Select(i => new Commit { SHA = $"merge-{i}" }).ToList();
        for (var i = 0; i < hidden.Count; i++)
            hidden[i].Parents = i + 1 < hidden.Count ? [hidden[i + 1].SHA, chain[i].SHA] : [chain[i].SHA];
        var mergeTip = new Commit { SHA = "merge-tip", Parents = [hidden[0].SHA] };
        var mergedHistory = new List<Commit> { mergeTip };
        mergedHistory.AddRange(hidden);
        mergedHistory.AddRange(chain);
        var mergedVisible = new List<Commit> { mergeTip };
        mergedVisible.AddRange(chain);
        graph = CommitGraph.GenerateFiltered(mergedHistory, mergedVisible, false, CommitGraphHighlighting.All, []);
        Check(graph.Paths.Count == 1 && graph.Dots.All(dot => dot.Center.X == 10), "hidden merges do not connect redundantly to every visible ancestor");
        Check(chain[0].Parents.Single() == chain[1].SHA && hidden[0].Parents.Count == 2, "removing redundant graph edges preserves real commit parents");

        var sparse = Enumerable.Range(0, 600).Select(i => new Commit { SHA = $"commit-{i}", Parents = [$"commit-{i + 1}"] }).ToList();
        var visible = sparse.Where((_, i) => i % 3 == 0).ToList();
        graph = CommitGraph.GenerateFiltered(sparse, visible, false, CommitGraphHighlighting.All, []);
        Check(graph.Paths.Count == 1 && graph.Dots.All(dot => dot.Center.X == 10), "hundreds of sparse matches do not accumulate phantom branches");
        graph = CommitGraph.Generate(sparse, false, CommitGraphHighlighting.All, []);
        Check(graph.Dots.Count == sparse.Count && graph.Dots.All(dot => dot.Center.X == 10), "unfiltered graph retains original topology");
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

    private static void SetWindowActive(Window window, bool active)
    {
        // Simulate the platform activation callback without taking native desktop focus.
        typeof(WindowBase).GetMethod(active ? "HandleActivated" : "HandleDeactivated",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(window, null);
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
