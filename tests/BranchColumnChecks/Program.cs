using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media.Imaging;
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
        var fixture = Path.Combine(Path.GetTempPath(), "sourcegit-branches-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        SourceGit.Native.OS.BasicDirectories.ConfigDir = fixture;
        SourceGit.Native.OS.BasicDirectories.CacheDir = fixture;
        Repository repo = null;
        Window window = null;
        try
        {
            App.BuildAvaloniaApp().SetupWithoutStarting();
            App.SetLocale("en_US");
            CheckBranchSummaryRendering();
            CheckGraphBranchContext();
            Git(fixture, "init -b main");
            Git(fixture, "config user.name Test");
            Git(fixture, "config user.email test@example.com");
            Git(fixture, "commit --allow-empty -m ancestor");
            Git(fixture, "commit --allow-empty -m subject");
            Git(fixture, "tag v1");
            Git(fixture, "update-ref refs/remotes/origin/main HEAD");
            repo = new Repository(false, fixture, Path.Combine(fixture, ".git"));
            repo.Open();
            window = new Window { Title = "SourceGit Branch Column Checks", Width = 1100, Height = 760,
                Content = new Views.Repository { DataContext = repo } };
            window.Show();
            PumpUntil(() => !repo.Histories.IsLoading && repo.Histories.Commits.Count == 2);
            CheckColumn(repo, window, fixture, args.Contains("--render"));
            Console.WriteLine($"PASS: {_checks} branch-column checks");
        }
        finally
        {
            window?.Close();
            repo?.Close();
            for (var attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    if (!Path.GetFullPath(fixture).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Fixture must stay inside the temporary directory");
                    foreach (var file in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
                        File.SetAttributes(file, FileAttributes.Normal);
                    Directory.Delete(fixture, true);
                    break;
                }
                catch (IOException) when (attempt < 9) { Thread.Sleep(50); }
                catch (UnauthorizedAccessException) when (attempt < 9) { Thread.Sleep(50); }
            }
        }
    }

    private static void CheckBranchSummaryRendering()
    {
        var commit = new Commit();
        commit.ParseDecorators("HEAD -> refs/heads/main, refs/remotes/origin/main, tag: refs/tags/v1");
        var label = new Views.CommitRefsPresenter { ShowBranchSummary = true, FontSize = 12, DataContext = commit };
        label.Measure(new Size(double.PositiveInfinity, 26));
        Check(label.BranchSummaryHasRemote && label.BranchSummaryText == "main", "matching local and remote use one local badge with both icons");
        Check(label.BranchSummaryAdditionalCount == 0 && label.BranchSummarySuffixWidth == 0, "matching local and remote count as one label");
        Check(Math.Abs(label.DesiredSize.Width - label.BranchSummaryRight - 1.5) <= 1,
            "single summary badge reserves no count space or multi-label gutter");
        var pairedWidth = label.BranchSummaryRight;
        var local = new Commit();
        local.ParseDecorators("HEAD -> refs/heads/main");
        label.DataContext = local;
        label.Measure(new Size(double.PositiveInfinity, 26));
        Check(Math.Abs(pairedWidth - label.BranchSummaryRight - 12) < 0.01, "combined badge reserves compact space for the cloud icon");
        label.DataContext = commit;
        commit.ParseDecorators("refs/heads/topic, refs/remotes/origin/topic, refs/remotes/upstream/topic, refs/remotes/origin/other");
        label.Measure(new Size(double.PositiveInfinity, 26));
        Check(label.BranchSummaryAdditionalCount == 2, "extra count groups matching branches even when they are not the displayed label");
        Check(Math.Abs(label.DesiredSize.Width - label.BranchSummaryRight - 1.5 - label.BranchSummarySuffixWidth) <= 1,
            "summary reserves postfix space only when additional labels exist");
        Check(commit.BranchToolTip.Contains("upstream/topic") && commit.BranchToolTip.Contains("origin/main"), "tooltip retains every grouped ref name");

        var remoteOnly = new Commit();
        remoteOnly.ParseDecorators("refs/remotes/origin/topic, refs/remotes/upstream/topic");
        label.DataContext = remoteOnly;
        label.Measure(new Size(double.PositiveInfinity, 26));
        Check(!label.BranchSummaryHasRemote && label.BranchSummaryAdditionalCount == 1, "remote refs stay separate when no matching local is on the commit");
        Check(label.BranchSummaryText == "origin/topic", "remote-only labels retain the remote prefix");

        var longName = "feature/" + string.Concat(Enumerable.Repeat("árvíztűrő-e\u0301-😀/", 15));
        var longCommit = new Commit();
        longCommit.ParseDecorators($"HEAD -> refs/heads/{longName}, refs/remotes/origin/{longName}, refs/heads/other");
        label.DataContext = longCommit;
        label.Measure(new Size(84, 26));
        label.Arrange(new Rect(0, 0, 84, 26));
        Check(label.BranchSummaryText.EndsWith("...", StringComparison.Ordinal), "long branch names use a trailing ellipsis");
        Check(label.BranchSummaryRight + label.BranchSummarySuffixWidth <= 84, "badge and external count fit inside a minimum-width column");
        Check(label.BranchSummaryHasRemote && label.BranchSummaryAdditionalCount == 1, "truncation preserves icons and the grouped count");
        Check(!label.BranchSummaryText.EnumerateRunes().Any(x => x.Value == 0xFFFD), "truncation preserves Unicode characters");
        Check(longCommit.BranchToolTip.Contains(longName), "tooltip retains the full untruncated branch name");
        label.Measure(new Size(4096, 26));
        label.Arrange(new Rect(0, 0, 4096, 26));
        Check(label.BranchSummaryText == longName, "widening the column restores the full name");
        label.Arrange(new Rect(0, 0, 84, 26));
        Check(label.BranchSummaryText.EndsWith("...", StringComparison.Ordinal) && label.BranchSummaryRight + label.BranchSummarySuffixWidth <= 84,
            "arrange also constrains a badge measured at a larger width");
        label.ShowBranchSummary = false;
        label.Measure(new Size(double.PositiveInfinity, 26));
        Check(!label.BranchSummaryHasRemote && label.BranchSummarySuffixWidth == 0 && label.DesiredSize.Width > 84,
            "other ref presenters retain their existing rendering behavior");
    }

    private static void CheckGraphBranchContext()
    {
        var root = new Commit { SHA = "root" };
        var mainParent = new Commit { SHA = "main-parent", Parents = [root.SHA] };
        var featureParent = new Commit { SHA = "feature-parent", Parents = [root.SHA] };
        var feature = new Commit { SHA = "feature", Parents = [featureParent.SHA] };
        feature.ParseDecorators("refs/heads/feature, refs/remotes/origin/feature");
        var main = new Commit { SHA = "main", Parents = [mainParent.SHA, feature.SHA] };
        main.ParseDecorators("HEAD -> refs/heads/main");
        CommitGraph.Generate([main, feature, mainParent, featureParent, root], false, CommitGraphHighlighting.All, []);
        Check(mainParent.GraphBranches.Single().Name == "main", "main ancestry inherits the main lane branch");
        Check(featureParent.GraphBranches.Select(x => x.Name).SequenceEqual(new[] { "feature", "origin/feature" }),
            "merged feature ancestry inherits its own lane branch and remote");
        Check(root.GraphBranches.Single().Name == "main", "shared ancestry follows the active branch lane");
        CommitGraph.Generate([root], false, CommitGraphHighlighting.All, []);
        Check(root.GraphBranches.Count == 0, "graph regeneration clears branch context when no known branch reaches a commit");
    }


    private static void CheckColumn(Repository repo, Window window, string fixture, bool render)
    {
        var vm = repo.Histories;
        var list = window.GetVisualDescendants().OfType<Views.HistoriesCommitList>().Single();
        var column = list.Columns[5];
        var graph = window.GetVisualDescendants().OfType<Views.CommitGraph>().Single();
        var connections = window.GetVisualDescendants().OfType<Views.CommitBranchConnections>().Single();
        Check(!vm.IsBranchesColumnVisible && !column.IsVisible, "branch column defaults to hidden");
        Check(column.DisplayIndex == 0 && list.Columns[0].DisplayIndex == 1, "branch column is before the graph without changing source indexes");
        vm.IsBranchesColumnVisible = true;
        PumpUntil(() => column.IsVisible && column.ActualWidth > 0 && graph.Margin.Left == column.ActualWidth);
        var tip = vm.Commits.Single(x => x.IsCurrentHead);
        var ancestor = vm.Commits.Single(x => !x.IsCurrentHead);
        var tipRow = list.GetVisualDescendants().OfType<DataGridRow>().Single(x => ReferenceEquals(x.DataContext, tip));
        var tipCell = tipRow.GetVisualDescendants().OfType<Border>().Single(x => x.Child is Views.CommitRefsPresenter { ShowBranchSummary: true });
        var label = (Views.CommitRefsPresenter)tipCell.Child;
        Check(label.BranchSummaryText == "main" && label.BranchSummaryHasRemote && label.BranchSummaryAdditionalCount == 0,
            "real history combines local and remote branch labels");
        Check(ToolTip.GetTip(tipCell) as string == "main\norigin/main", "branch cell tooltip retains all full ref names");
        var message = tipRow.GetVisualDescendants().OfType<Views.CommitMessagePanel>().Single();
        var messageRefs = (Views.CommitRefsPresenter)message.Children[0];
        Check(!messageRefs.ShowBranches && messageRefs.DecoratorAt(new Point(4, 4))?.Type == DecoratorType.Tag,
            "branch column hides message branch labels while preserving tags");
        Check(message.Children[0].Bounds.Left == message.Children[1].Bounds.Right + 4, "tags follow immediately after the subject");
        PumpUntil(() => connections.Connections.Count == 1);
        var connection = connections.Connections[0];
        Check(connection.IsActive && connection.Start.Y == connection.End.Y, "active branch connects horizontally at full opacity");
        var dot = vm.Graph.Dots[0];
        var expectedEnd = graph.TranslatePoint(new Point(dot.Center.X, dot.Center.Y * graph.Layout.RowHeight - graph.Layout.StartY), connections).Value;
        Check(connection.End == expectedEnd, "connection ends at the commit graph dot");

        var row = list.GetVisualDescendants().OfType<DataGridRow>().Single(x => ReferenceEquals(x.DataContext, ancestor));
        var cell = row.GetVisualDescendants().OfType<Border>().Single(x => x.Child is Views.CommitRefsPresenter { ShowBranchSummary: true });
        var hover = (Views.CommitRefsPresenter)cell.Child;
        Check(hover.BranchSummaryRight == 0, "unlabeled commits stay blank before hover");
        var pointer = new Pointer(8, PointerType.Mouse, true);
        var position = cell.TranslatePoint(new Point(10, 10), window).Value;
        cell.RaiseEvent(new PointerEventArgs(InputElement.PointerEnteredEvent, cell, pointer, window, position, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None));
        PumpUntil(() => hover.BranchSummaryText == "main");
        Check(hover.Opacity == 0.7 && hover.BranchSummaryHasRemote, "hover badge uses lane identity, combined icons, and 70 percent opacity");
        Check(hover.DecoratorAt(new Point(4, 4))?.Type == DecoratorType.LocalBranchHead && ancestor.Decorators.Count == 0,
            "hover never invents HEAD or branch refs on an ancestor");
        Check(connections.Connections.Count == 1, "hover badge never adds a connector");
        if (render)
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize(1100, 760));
            bitmap.Render(window);
            bitmap.Save(Path.Combine(AppContext.BaseDirectory, "preview-branches.png"));
        }
        cell.RaiseEvent(new PointerEventArgs(InputElement.PointerExitedEvent, cell, pointer, window, position, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None));
        PumpUntil(() => hover.BranchSummaryRight == 0);
        Check(hover.BranchSummaryRefs == null && ToolTip.GetTip(cell) == null, "leaving the branch cell clears the hover badge and tooltip");

        var handle = window.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "BranchesColumnResizeHandle");
        var start = handle.TranslatePoint(new Point(2, 12), window).Value;
        var end = start + new Vector(60, 0);
        var originalWidth = column.ActualWidth;
        Check(ReferenceEquals(window.InputHitTest(start), handle), "branch resize handle is reachable");
        handle.RaiseEvent(new PointerPressedEventArgs(handle, pointer, window, start, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        handle.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, handle, pointer, window, end, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None));
        handle.RaiseEvent(new PointerReleasedEventArgs(handle, pointer, window, end, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
        PumpUntil(() => column.ActualWidth > originalWidth + 50);
        Check(vm.BranchesColumnWidth == column.ActualWidth, "dragging the branch header saves its width");
        PumpUntil(() => connections.Connections[0].End.X > connection.End.X + 50);
        Check(Math.Abs(connections.Connections[0].End.X - connection.End.X - 60) < 0.01, "graph and connector follow column resizing");
        repo.UIStates.Save();
        var saved = RepositoryUIStates.Load(Path.Combine(fixture, ".git"));
        Check(saved.IsBranchesColumnVisibleInHistory && saved.BranchesColumnWidth == column.ActualWidth,
            "branch visibility and width persist per repository");
        var restored = new Views.Histories { DataContext = vm };
        Check(restored.FindControl<Views.HistoriesCommitList>("CommitListContainer").Columns[5].Width.Value == saved.BranchesColumnWidth,
            "new history views restore the saved width");
        vm.IsBranchesColumnVisible = false;
        PumpUntil(() => !column.IsVisible && graph.Margin.Left == 0 && connections.Connections.Count == 0);
        Check(messageRefs.ShowBranches && messageRefs.DecoratorAt(new Point(4, 4))?.IsBranch == true,
            "hiding the column restores message branch labels and graph position");
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
