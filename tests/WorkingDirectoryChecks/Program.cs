using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SourceGit;
using SourceGit.Models;
using SourceGit.ViewModels;
using Views = SourceGit.Views;

internal static class Program
{
    private static int _checks;

    [STAThread]
    private static void Main(string[] args)
    {
        var fixture = Path.Combine(Path.GetTempPath(), "sourcegit-working-directory-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        SourceGit.Native.OS.BasicDirectories.ConfigDir = fixture;
        SourceGit.Native.OS.BasicDirectories.CacheDir = fixture;
        Repository repo = null;
        Window window = null;
        try
        {
            App.BuildAvaloniaApp().SetupWithoutStarting();
            CheckHeadPreference(fixture);
            Preferences.Instance.WorkingDirectoryHeadMode = 1;
            CheckClassification();
            CheckGraph();
            Git(fixture, "init -b main");
            Git(fixture, "config user.name Test");
            Git(fixture, "config user.email test@example.com");
            File.WriteAllText(Path.Combine(fixture, "tracked.txt"), "initial\n");
            Git(fixture, "add tracked.txt");
            Git(fixture, "commit -m initial");
            File.WriteAllText(Path.Combine(fixture, "tracked.txt"), "edited\n");
            repo = new Repository(false, fixture, Path.Combine(fixture, ".git"));
            repo.Open();
            window = new Window { Title = "SourceGit Working Directory Checks", Width = 1100, Height = 800,
                Content = new Views.Repository { DataContext = repo } };
            window.Show();
            PumpUntil(() => !repo.Histories.IsLoading && repo.LocalChangesCount == 1);
            CheckSelection(repo, window);
            CheckStatusPipeline(repo, fixture);
            CheckCommitRefreshRace(repo, fixture);
            CheckHeadModes(repo, window);
            CheckLayouts(repo, window, args.Contains("--render"));
            CheckHeadDropdown(args.Contains("--render"));
            Console.WriteLine($"PASS: {_checks} working-directory checks");
            if (args.Contains("--ui"))
            {
                using var cancellation = new CancellationTokenSource();
                window.Closed += (_, _) => cancellation.Cancel();
                Dispatcher.UIThread.MainLoop(cancellation.Token);
            }
        }
        finally
        {
            window?.Close();
            repo?.Close();
            foreach (var file in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(fixture, true);
        }
    }

    private static void CheckHeadPreference(string fixture)
    {
        var pref = Preferences.Instance;
        Check(pref.WorkingDirectoryHeadMode == 0 && new Preferences().WorkingDirectoryHeadMode == 0, "existing preferences default to Hide");
        pref.SetCanModify();
        foreach (var mode in new[] { 0, 1, 2 })
        {
            pref.WorkingDirectoryHeadMode = mode;
            pref.Save();
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture, "preference.json")));
            Check(json.RootElement.GetProperty("WorkingDirectoryHeadMode").GetInt32() == mode, "all head modes persist through the production serializer");
        }
        pref.WorkingDirectoryHeadMode = -1;
        Check(pref.WorkingDirectoryHeadMode == 2, "unselected dropdown value is ignored");
        pref.WorkingDirectoryHeadMode = 3;
        Check(pref.WorkingDirectoryHeadMode == 2, "invalid stored mode is ignored");
        File.Delete(Path.Combine(fixture, "preference.json"));
    }

    private static void CheckHeadModes(Repository repo, Window window)
    {
        var vm = repo.Histories;
        var pref = Preferences.Instance;
        var list = window.GetVisualDescendants().OfType<Views.HistoriesCommitList>().Single();
        using var second = new Histories(repo);
        second.Commits = new List<Commit>(vm.Commits);
        second.UpdateWorkingDirectory([new Change { Path = "tracked.txt", WorkTree = ChangeState.Modified }]);
        pref.WorkingDirectoryHeadMode = 0;
        PumpUntil(() => !vm.Entries.Contains(vm.WorkingDirectory));
        Check(vm.Entries.SequenceEqual(vm.Commits.Cast<object>()) && vm.Graph.Dots.Count == vm.Commits.Count, "Hide contains only real commits and graph nodes");
        Check(!vm.IsWorkingDirectorySelected && vm.DetailContext is CommitDetail && list.SelectedItem is Commit, "hiding selected working directory restores real commit details");
        Check(!second.IsWorkingDirectoryVisible && !second.Entries.Contains(second.WorkingDirectory), "preference changes update every open history");
        vm.UpdateWorkingDirectory([new Change { Path = "new.txt", WorkTree = ChangeState.Untracked }]);
        Check(!vm.Entries.Contains(vm.WorkingDirectory), "status refresh cannot reintroduce hidden head");
        vm.Commits = new List<Commit>(vm.Commits);
        Check(vm.Graph.Dots.Count == vm.Commits.Count && !vm.Entries.Contains(vm.WorkingDirectory), "history refresh cannot reintroduce hidden head");
        vm.IsWorkingDirectorySelected = true;
        Check(!vm.IsWorkingDirectorySelected, "hidden head cannot be selected programmatically");
        pref.WorkingDirectoryHeadMode = 1;
        PumpUntil(() => vm.Entries.Contains(vm.WorkingDirectory));
        Check(vm.WorkingDirectory.Added == 1 && second.Entries.Contains(second.WorkingDirectory), "conditional mode shows latest changes across histories");
        vm.UpdateWorkingDirectory([]);
        Check(!vm.IsWorkingDirectoryVisible && vm.Graph.Dots.Count == vm.Commits.Count, "conditional mode removes clean head");
        pref.WorkingDirectoryHeadMode = 2;
        PumpUntil(() => vm.Entries.Contains(vm.WorkingDirectory));
        Check(vm.IsWorkingDirectoryVisible && vm.WorkingDirectory.Modified + vm.WorkingDirectory.Added + vm.WorkingDirectory.Deleted == 0, "Always shows a clean working directory");
        list.SelectedItem = vm.WorkingDirectory;
        PumpUntil(() => vm.IsWorkingDirectorySelected);
        var graph = vm.Graph;
        vm.UpdateWorkingDirectory([new Change { Path = "tracked.txt", WorkTree = ChangeState.Modified }]);
        vm.UpdateWorkingDirectory([]);
        Check(vm.IsWorkingDirectorySelected && ReferenceEquals(vm.DetailContext, repo.WorkingCopy) && ReferenceEquals(graph, vm.Graph), "Always preserves editor selection and graph across dirty/clean transitions");
        vm.Commits = new List<Commit>(vm.Commits);
        Check(vm.Entries.Count == vm.Commits.Count + 1 && vm.Graph.Dots.Count == vm.Entries.Count, "Always survives history refresh");
        var commits = vm.Commits;
        vm.Commits = [];
        Check(vm.Entries.Count == 1 && vm.Graph.Dots.Count == 1, "Always shows a clean head in empty or filtered history");
        pref.WorkingDirectoryHeadMode = 0;
        Check(vm.Entries.Count == 0 && vm.Graph.Dots.Count == 0 && vm.DetailContext is SourceGit.Models.Null, "Hide restores empty history without stale editor details");
        vm.Commits = commits;
        using (var bare = new Histories(new Repository(true, repo.FullPath, repo.GitDir)))
        {
            pref.WorkingDirectoryHeadMode = 2;
            bare.Commits = [];
            bare.UpdateWorkingDirectory([new Change { Path = "tracked.txt", WorkTree = ChangeState.Modified }]);
            Check(!bare.IsWorkingDirectoryVisible && bare.Entries.Count == 0 && bare.Graph.Dots.Count == 0, "bare repositories never get a working directory head");
        }
        second.Dispose();
        var closedGraph = second.Graph;
        pref.WorkingDirectoryHeadMode = 1;
        Check(ReferenceEquals(second.Graph, closedGraph), "closed histories unsubscribe from preferences");
        vm.UpdateWorkingDirectory([new Change { Path = "tracked.txt", WorkTree = ChangeState.Modified }]);
        list.SelectedItem = vm.WorkingDirectory;
        PumpUntil(() => vm.IsWorkingDirectorySelected);
    }

    private static void CheckHeadDropdown(bool render)
    {
        var pref = Preferences.Instance;
        var mode = pref.WorkingDirectoryHeadMode;
        var dialog = new Views.Preferences();
        dialog.Show();
        var tabs = dialog.GetVisualDescendants().OfType<TabControl>().First();
        tabs.SelectedIndex = 1;
        PumpUntil(() => dialog.GetVisualDescendants().OfType<ComboBox>().Any(x => Avalonia.Automation.AutomationProperties.GetName(x) == "Working Directory Head"));
        var dropdown = dialog.GetVisualDescendants().OfType<ComboBox>().Single(x => Avalonia.Automation.AutomationProperties.GetName(x) == "Working Directory Head");
        Check(dropdown.Items.OfType<ComboBoxItem>().Select(x => x.Content as string).SequenceEqual(new[] { "Hide", "Show when there are local changes", "Show always" }), "Appearance dropdown has all three choices in order");
        foreach (var selected in new[] { 0, 1, 2 })
        {
            dropdown.SelectedIndex = selected;
            Check(pref.WorkingDirectoryHeadMode == selected, "dropdown selection updates the preference");
        }
        pref.WorkingDirectoryHeadMode = 0;
        Check(dropdown.SelectedIndex == 0, "preference updates the dropdown selection");
        var box = dialog.GetVisualDescendants().OfType<CheckBox>().First(x => x.Content as string == App.Text("Preferences.Appearance.UseFixedTabWidth"));
        var dropdownPosition = dropdown.TranslatePoint(new Point(), dialog);
        var checkboxPosition = box.TranslatePoint(new Point(), dialog);
        Check(dropdownPosition.HasValue && checkboxPosition.HasValue && dropdownPosition.Value.Y + dropdown.Bounds.Height <= checkboxPosition.Value.Y, "head dropdown appears above Appearance checkboxes");
        if (render)
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)dialog.Bounds.Width, (int)dialog.Bounds.Height));
            bitmap.Render(dialog);
            bitmap.Save(Path.Combine(AppContext.BaseDirectory, "appearance-working-directory-head.png"));
        }
        // Hide rather than close: closing Preferences writes global Git configuration.
        dialog.Hide();
        pref.WorkingDirectoryHeadMode = mode;
    }

    private static void CheckClassification()
    {
        var entry = new WorkingDirectoryEntry();
        var cases = new (ChangeState Index, ChangeState WorkTree, int Modified, int Added, int Deleted)[]
        {
            (ChangeState.None, ChangeState.Modified, 1, 0, 0),
            (ChangeState.Modified, ChangeState.Modified, 1, 0, 0),
            (ChangeState.Added, ChangeState.Modified, 0, 1, 0),
            (ChangeState.Added, ChangeState.Deleted, 0, 1, 0),
            (ChangeState.None, ChangeState.Untracked, 0, 1, 0),
            (ChangeState.Deleted, ChangeState.None, 0, 0, 1),
            (ChangeState.Modified, ChangeState.Deleted, 0, 0, 1),
            (ChangeState.Renamed, ChangeState.Deleted, 1, 0, 0),
            (ChangeState.Added, ChangeState.TypeChanged, 1, 0, 0),
            (ChangeState.None, ChangeState.Conflicted, 1, 0, 0),
        };
        foreach (var c in cases)
        {
            entry.SetChanges([new Change { Path = "file", Index = c.Index, WorkTree = c.WorkTree }]);
            Check((entry.Modified, entry.Added, entry.Deleted) == (c.Modified, c.Added, c.Deleted), $"classification {c.Index}/{c.WorkTree}");
        }
        entry.SetChanges([new Change { Path = "file", Index = ChangeState.Modified, WorkTree = ChangeState.Modified },
            new Change { Path = "file", Index = ChangeState.Modified }]);
        Check(entry.Modified == 1, "deduplicate paths");
        Check(entry.Summary == "* Working Directory *", "fixed working-directory label");
        entry.SetChanges([]);
        Check(!entry.HasChanges, "clean entry has no changes");
    }

    private static void CheckGraph()
    {
        CommitGraph.SetDefaultPens();
        foreach (var headType in new[] { DecoratorType.CurrentBranchHead, DecoratorType.CurrentCommitHead })
        {
            var head = new Commit { SHA = new string('a', 40), Decorators = [new SourceGit.Models.Decorator { Type = headType }] };
            var tip = new Commit { SHA = new string('b', 40), Parents = [head.SHA] };
            var baseline = CommitGraph.Generate([tip, head], false, CommitGraphHighlighting.All, []);
            var margins = new[] { tip.LeftMargin, head.LeftMargin };
            var graph = CommitGraph.Generate([tip, head], false, CommitGraphHighlighting.All, [], true);
            Check(graph.Dots[0].Center.X == baseline.Dots[1].Center.X, "working node shares pinned HEAD lane");
            Check(graph.Dots.Skip(1).Select(x => x.Center.X).SequenceEqual(baseline.Dots.Select(x => x.Center.X)), "working row preserves every real node X coordinate");
            Check(tip.LeftMargin == margins[0] && head.LeftMargin == margins[1], "working row preserves commit text margins");
            Check(graph.Paths[0].Points.All(x => x.X == 10), "working edge stays straight in the leftmost lane");
            Check(graph.Dots[1].Center.X > graph.Dots[2].Center.X, "newer branch tip stays right of HEAD");
            Check(graph.Dots.Count == 3 && graph.Dots[0].Type == CommitGraph.DotType.WorkingDirectory, "distinct graph node");
            Check(graph.Dots[2].Type == CommitGraph.DotType.Head, "preserve real HEAD");
            Check(graph.Paths[0].Points[^1] == graph.Dots[2].Center, "connect HEAD below tip");
            Check(graph.Dots[1].Center.Y == 1.5 && graph.Dots[2].Center.Y == 2.5, "row alignment");
            var filtered = CommitGraph.Generate([tip], false, CommitGraphHighlighting.All, [], true);
            Check(filtered.Paths.All(x => x.Points[0] != filtered.Dots[0].Center), "filtered HEAD has no working edge");
        }
        var sideHead = new Commit { SHA = new string('c', 40), Decorators = [new SourceGit.Models.Decorator { Type = DecoratorType.CurrentBranchHead }] };
        var otherTip = new Commit { SHA = new string('d', 40), Parents = [new string('e', 40)] };
        var sideBaseline = CommitGraph.Generate([otherTip, sideHead], false, CommitGraphHighlighting.All, []);
        var sideGraph = CommitGraph.Generate([otherTip, sideHead], false, CommitGraphHighlighting.All, [], true);
        Check(sideGraph.Dots[0].Center.X == sideBaseline.Dots[1].Center.X && sideGraph.Dots[0].Center.X < sideGraph.Dots[1].Center.X, "HEAD is leftmost even below another branch tip");
        var newestHead = CommitGraph.Generate([sideHead], false, CommitGraphHighlighting.All, [], true);
        Check(newestHead.Paths[0].Points.All(x => x.X == newestHead.Dots[0].Center.X), "newest HEAD uses a vertical edge");
        var legacyHead = new Commit { SHA = "legacy-head", Decorators = [new SourceGit.Models.Decorator { Type = DecoratorType.CurrentBranchHead }] };
        var legacyTip = new Commit { SHA = "legacy-tip", Parents = [legacyHead.SHA] };
        var legacy = CommitGraph.Generate([legacyTip, legacyHead], false, CommitGraphHighlighting.All, [], false, false);
        Check(legacy.Dots.Count == 2 && legacy.Dots.All(x => x.Center.X == 10), "Hide restores fork-point lanes without reserving HEAD lane");
        Check(legacy.Dots[0].Center.Y == 0.5 && legacy.Dots[1].Center.Y == 1.5, "Hide restores original row positions");
        CheckPinnedAncestry();
        var empty = CommitGraph.Generate([], false, CommitGraphHighlighting.All, [], true);
        Check(empty.Dots.Count == 1 && empty.Paths.Count == 0, "unborn repository graph");
    }

    private static void CheckPinnedAncestry()
    {
        var root = new Commit { SHA = "root" };
        var parent = new Commit { SHA = "parent", Parents = [root.SHA] };
        var side = new Commit { SHA = "side", Parents = [root.SHA] };
        var merge = new Commit { SHA = "merge", Parents = [parent.SHA, side.SHA] };
        var head = new Commit { SHA = "head", Parents = [merge.SHA], Decorators = [new SourceGit.Models.Decorator { Type = DecoratorType.CurrentBranchHead }] };
        var other = new Commit { SHA = "other", Parents = [root.SHA] };
        var tip = new Commit { SHA = "tip", Parents = [other.SHA] };
        List<Commit> commits = [tip, head, other, merge, parent, side, root];
        foreach (var dirty in new[] { false, true })
        foreach (var firstParentOnly in new[] { false, true })
        foreach (var highlighting in Enum.GetValues<CommitGraphHighlighting>())
        {
            var graph = CommitGraph.Generate(commits, firstParentOnly, highlighting, [head.SHA], dirty);
            var offset = dirty ? 1 : 0;
            Check(new[] { 1, 3, 4, 6 }.All(i => graph.Dots[i + offset].Center.X == 10), "HEAD first-parent ancestry stays leftmost and straight");
            Check(new[] { 0, 2, 5 }.All(i => graph.Dots[i + offset].Center.X > 10), "other branches remain to the right of HEAD ancestry");
            if (!dirty)
                Check(graph.Paths.Where(x => x.Color == graph.Dots[1].Color).All(x => x.Points[0].Y >= 1.5), "clean graph has no invented edge above HEAD");
        }
    }

    private static void CheckSelection(Repository repo, Window window)
    {
        var vm = repo.Histories;
        var list = window.GetVisualDescendants().OfType<Views.HistoriesCommitList>().Single();
        Check(vm.Commits.Count == 1 && vm.Entries.Count == 2, "real commit count excludes working row");
        list.SelectedItem = vm.WorkingDirectory;
        PumpUntil(() => vm.IsWorkingDirectorySelected);
        Check(ReferenceEquals(vm.DetailContext, repo.WorkingCopy) && vm.SelectedCommits.Count == 0, "working selection uses shared model");
        PumpUntil(() => window.GetVisualDescendants().OfType<Views.WorkingCopy>().Any());
        var editor = window.GetVisualDescendants().OfType<Views.WorkingCopy>().Single();
        list.Focus();
        list.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
        PumpUntil(() => vm.DetailContext is CommitDetail);
        Check(!vm.IsWorkingDirectorySelected, "keyboard Down selects a real commit");
        list.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Up });
        PumpUntil(() => vm.IsWorkingDirectorySelected);
        Check(vm.SelectedCommits.Count == 0, "keyboard Up selects working directory exclusively");
        list.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.A, KeyModifiers = KeyModifiers.Control });
        PumpUntil(() => !vm.IsWorkingDirectorySelected);
        Check(list.SelectedItems.Count == vm.Commits.Count && vm.SelectedCommits.Count == vm.Commits.Count, "Select All contains only real commits");
        list.SelectedItem = vm.WorkingDirectory;
        PumpUntil(() => vm.IsWorkingDirectorySelected);
        var graph = vm.Graph;
        repo.WorkingCopy.CommitMessage = "Live subject\nBody";
        Check(vm.WorkingDirectory.Summary == "* Working Directory *" && ReferenceEquals(vm.Graph, graph), "draft preserves fixed label and graph");
        repo.SelectedViewIndex = 1;
        PumpUntil(() => window.GetVisualDescendants().OfType<Views.WorkingCopy>().Any());
        Check(ReferenceEquals(editor, window.GetVisualDescendants().OfType<Views.WorkingCopy>().Single()), "same editor on sidebar");
        repo.SelectedViewIndex = 0;
        PumpUntil(() => window.GetVisualDescendants().OfType<Views.WorkingCopy>().Any());
        Check(ReferenceEquals(editor, window.GetVisualDescendants().OfType<Views.WorkingCopy>().Single()), "same editor in history");
        vm.IsCollapseDetails = true;
        PumpUntil(() => !window.GetVisualDescendants().OfType<Views.WorkingCopy>().Any());
        Check(vm.IsWorkingDirectorySelected, "collapse detaches editor while preserving selection");
        vm.IsCollapseDetails = false;
        PumpUntil(() => window.GetVisualDescendants().OfType<Views.WorkingCopy>().Any());
        Check(ReferenceEquals(editor, window.GetVisualDescendants().OfType<Views.WorkingCopy>().Single()), "expand restores shared editor");
        list.SelectedItem = vm.Commits[0];
        PumpUntil(() => vm.DetailContext is CommitDetail);
        Check(!vm.IsWorkingDirectorySelected, "real commit restores details");
        list.SelectedItem = vm.WorkingDirectory;
        PumpUntil(() => vm.IsWorkingDirectorySelected);
        vm.NavigateTo(vm.Commits[0].SHA);
        PumpUntil(() => list.SelectedItem is Commit);
        Check(!vm.IsWorkingDirectorySelected, "programmatic navigation restores real selection");
        list.SelectedItems.Add(vm.WorkingDirectory);
        PumpUntil(() => vm.IsWorkingDirectorySelected);
        Check(list.SelectedItems.Count == 1 && vm.SelectedCommits.Count == 0, "working row exclusive selection");
        vm.UpdateWorkingDirectory([new Change { Path = "tracked.txt", WorkTree = ChangeState.Modified },
            new Change { Path = "new.txt", WorkTree = ChangeState.Untracked }]);
        Check(vm.IsWorkingDirectorySelected && ReferenceEquals(graph, vm.Graph), "count update preserves selection and graph");
        vm.Commits = new List<Commit>(vm.Commits);
        PumpUntil(() => list.SelectedItems.Contains(vm.WorkingDirectory));
        Check(vm.IsWorkingDirectorySelected, "history refresh preserves working selection");
        vm.UpdateWorkingDirectory([]);
        PumpUntil(() => vm.DetailContext is CommitDetail);
        Check(vm.SelectedCommits.Count == 1 && vm.SelectedCommits[0].IsCurrentHead, "clean transition selects HEAD");
        vm.UpdateWorkingDirectory([new Change { Path = "tracked.txt", WorkTree = ChangeState.Modified }]);
        list.SelectedItem = vm.WorkingDirectory;
        PumpUntil(() => vm.IsWorkingDirectorySelected);
        var commits = vm.Commits;
        vm.Commits = [];
        Check(vm.Entries.Count == 1 && vm.Graph.Dots.Count == 1, "working entry survives empty or filtered history");
        vm.UpdateWorkingDirectory([]);
        PumpUntil(() => vm.DetailContext is SourceGit.Models.Null);
        Check(list.SelectedItems.Count == 0, "clean transition without displayed HEAD clears details");
        vm.Commits = commits;
        vm.UpdateWorkingDirectory([new Change { Path = "tracked.txt", WorkTree = ChangeState.Modified }]);
        list.SelectedItem = vm.WorkingDirectory;
        PumpUntil(() => vm.IsWorkingDirectorySelected);
    }

    private static void CheckStatusPipeline(Repository repo, string fixture)
    {
        File.WriteAllText(Path.Combine(fixture, "new.txt"), "added\n");
        repo.RefreshWorkingCopyChanges();
        PumpUntil(() => repo.LocalChangesCount == 2);
        Check(repo.Histories.WorkingDirectory.Added == 1 && repo.Histories.WorkingDirectory.Modified == 1, "status snapshot counts untracked once");
        repo.IncludeUntracked = false;
        PumpUntil(() => repo.LocalChangesCount == 1);
        Check(repo.Histories.WorkingDirectory.Added == 0, "untracked preference shared with badge");
        repo.IncludeUntracked = true;
        PumpUntil(() => repo.LocalChangesCount == 2);
        Git(fixture, "add new.txt");
        File.AppendAllText(Path.Combine(fixture, "new.txt"), "edited after staging\n");
        repo.RefreshWorkingCopyChanges();
        PumpUntil(() => repo.WorkingCopy.Staged.Any(x => x.Path == "new.txt") && repo.WorkingCopy.Unstaged.Any(x => x.Path == "new.txt"));
        Check(repo.Histories.WorkingDirectory.Added == 1 && repo.Histories.WorkingDirectory.Modified == 1, "AM counts as one addition");
        Git(fixture, "mv tracked.txt renamed.txt");
        File.Delete(Path.Combine(fixture, "renamed.txt"));
        repo.RefreshWorkingCopyChanges();
        PumpUntil(() => repo.WorkingCopy.Staged.Any(x => x.Index == ChangeState.Renamed));
        Check(repo.Histories.WorkingDirectory.Modified == 1 && repo.Histories.WorkingDirectory.Deleted == 0, "rename plus deletion counts as modified");
        // Restore the isolated fixture to check a real watcher-driven dirty-to-clean transition.
        Git(fixture, "reset --hard HEAD");
        repo.RefreshWorkingCopyChanges();
        PumpUntil(() => repo.LocalChangesCount == 0);
        Check(!repo.Histories.WorkingDirectory.HasChanges && !repo.Histories.IsWorkingDirectorySelected, "refresh pipeline removes clean row");
        Check(repo.WorkingCopy.CommitMessage == "Live subject\nBody", "status refresh preserves draft");
        File.AppendAllText(Path.Combine(fixture, "tracked.txt"), "dirty again\n");
        repo.RefreshWorkingCopyChanges();
        PumpUntil(() => repo.LocalChangesCount == 1);
        Check(repo.Histories.Entries.Count == repo.Histories.Commits.Count + 1, "clean-to-dirty restores row");
        repo.Histories.IsWorkingDirectorySelected = true;
    }

    private static void CheckCommitRefreshRace(Repository repo, string fixture)
    {
        using (repo.LockWatcher())
        {
            Git(fixture, "add tracked.txt");
            Git(fixture, "commit -m second");
            // Deliberately deliver the clean status snapshot before the new commit query.
            repo.Histories.UpdateWorkingDirectory([]);
            repo.RefreshCommits();
            PumpUntil(() => repo.Histories.Commits.Count == 2);
            Check(repo.Histories.SelectedCommits is { Count: 1 } selected && selected[0].IsCurrentHead && selected[0].Subject == "second", "commit refresh selects new HEAD after earlier clean status");
            repo.RefreshWorkingCopyChanges();
            PumpUntil(() => repo.LocalChangesCount == 0);
        }
        File.AppendAllText(Path.Combine(fixture, "tracked.txt"), "preview edit\n");
        repo.RefreshWorkingCopyChanges();
        PumpUntil(() => repo.LocalChangesCount == 1);
        repo.Histories.IsWorkingDirectorySelected = true;
    }

    private static void CheckLayouts(Repository repo, Window window, bool render)
    {
        foreach (var horizontal in new[] { false, true })
        foreach (var theme in new[] { "Light", "Dark" })
        foreach (var width in new[] { 1100, 800 })
        {
            Preferences.Instance.UseTwoColumnsLayoutInHistories = horizontal;
            Preferences.Instance.Theme = theme;
            window.Width = width;
            // Give native layout/render jobs an opportunity to run.
            for (var i = 0; i < 5; i++)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(20);
            }
            window.Measure(new Size(width, 800));
            window.Arrange(new Rect(0, 0, width, 800));
            var editor = window.GetVisualDescendants().OfType<Views.CommitMessageToolBox>().Single();
            Check(editor.Bounds.Width >= 180 && editor.Bounds.Height > 50, $"editor usable: {theme}, horizontal={horizontal}, width={width}");
            Check(window.GetVisualDescendants().OfType<Views.WorkingCopy>().Count() == 1, "only one attached working copy");
            if (render)
            {
                using var bitmap = new RenderTargetBitmap(new PixelSize(width, 800));
                bitmap.Render(window);
                bitmap.Save(Path.Combine(AppContext.BaseDirectory, $"preview-{theme}-{horizontal}-{width}.png"));
            }
        }
    }

    private static void PumpUntil(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        do
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
            if (timer.Elapsed.TotalSeconds > 15)
                throw new Exception("Timed out waiting for UI state");
        } while (!condition());
        Dispatcher.UIThread.RunJobs();
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception("FAIL: " + name);
        _checks++;
    }

    private static void Git(string path, string args)
    {
        using var process = Process.Start(new ProcessStartInfo("git", args) { WorkingDirectory = path,
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new Exception(process.StandardError.ReadToEnd());
    }
}
