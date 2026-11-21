using System.Reflection;
using Avalonia;
using Avalonia.Media;
using Avalonia.Data;
using SourceGit;
using SourceGit.Models;
using SourceGit.ViewModels;
using Commit = SourceGit.Models.Commit;

internal static class Program
{
    private static int _checks;

    [STAThread]
    private static void Main()
    {
        CommitGraph.SetDefaultPens();
        CheckColorsAndHighlighting();
        CheckOlderHead();
        CheckFallbacks();
        CheckCustomPalettes();
        CheckStackedBookmarkColors();
        CheckLiveBookmarks();
        Console.WriteLine($"PASS: {_checks} bookmark branch color checks");
    }

    private static void CheckColorsAndHighlighting()
    {
        for (var bookmark = 1; bookmark < Bookmarks.Brushes.Length; bookmark++)
        foreach (var highlighting in Enum.GetValues<CommitGraphHighlighting>())
        foreach (var firstParentOnly in new[] { false, true })
        {
            var root = new Commit { SHA = "root", IsMerged = true };
            var mainParent = Commit("main-parent", root);
            mainParent.IsMerged = true;
            var featureParent = Commit("feature-parent", root);
            var feature = Commit("feature", featureParent, "refs/heads/feature");
            var head = Commit("head", mainParent, "HEAD -> refs/heads/main");
            head.Parents.Add(feature.SHA);
            List<Commit> commits = [head, feature, mainParent, featureParent, root];
            var baseline = CommitGraph.Generate(commits, firstParentOnly, highlighting, [feature.SHA]);
            var margins = commits.Select(x => x.LeftMargin).ToArray();
            var graph = CommitGraph.Generate(commits, firstParentOnly, highlighting, [feature.SHA], bookmark);
            Check(BrushColor(head.Color) == ((ISolidColorBrush)Bookmarks.Get(bookmark)).Color,
                "the current branch uses the exact repository bookmark color");
            Check(mainParent.Color == head.Color && root.Color == head.Color && feature.Color != head.Color,
                "the first-parent branch keeps its bookmark color while the merged branch stays distinct");
            Check(baseline.Dots.Select(x => x.IsHighlighted).SequenceEqual(graph.Dots.Select(x => x.IsHighlighted)) &&
                baseline.Paths.Select(x => x.IsHighlighted).SequenceEqual(graph.Paths.Select(x => x.IsHighlighted)),
                "bookmark colors preserve highlighted and grayed-out sections in every mode");
            Check(baseline.Dots.Select(x => x.Center).SequenceEqual(graph.Dots.Select(x => x.Center)) &&
                commits.Select(x => x.LeftMargin).SequenceEqual(margins),
                "bookmark colors preserve lane positions and graph width");
        }
    }

    private static void CheckOlderHead()
    {
        for (var bookmark = 1; bookmark < Bookmarks.Brushes.Length; bookmark++)
        {
            var root = new Commit { SHA = "root" };
            var head = Commit("head", root, "HEAD -> refs/heads/main");
            var tip = Commit("tip", head, "refs/heads/feature");
            var graph = CommitGraph.Generate([tip, head, root], false, CommitGraphHighlighting.All, [], bookmark);
            Check(BrushColor(head.Color) == ((ISolidColorBrush)Bookmarks.Get(bookmark)).Color && tip.Color != head.Color,
                "an older HEAD switches to the bookmark color without recoloring the branch above it");
            Check(graph.Paths.Count == 2 && graph.Paths[0].Points[^1] == graph.Dots[1].Center &&
                graph.Paths[1].Points[0] == graph.Dots[1].Center && graph.Dots.All(x => x.Center.X == 10),
                "the color boundary meets the older HEAD dot without moving the lane");

            root.Decorators = [new Decorator { Type = DecoratorType.CurrentBranchHead, Name = "main" }];
            head.Decorators.Clear();
            graph = CommitGraph.Generate([tip, head, root], false, CommitGraphHighlighting.All, [], bookmark);
            Check(BrushColor(root.Color) == ((ISolidColorBrush)Bookmarks.Get(bookmark)).Color && graph.Paths.Count == 1,
                "a root HEAD gets the bookmark color without adding an empty path");

            var detached = new Commit { SHA = "detached" };
            detached.ParseDecorators("HEAD");
            graph = CommitGraph.Generate([detached], false, CommitGraphHighlighting.All, [], bookmark);
            Check(graph.Dots.Count == 1 && graph.Paths.Count == 0 && BrushColor(detached.Color) == ((ISolidColorBrush)Bookmarks.Get(bookmark)).Color,
                "a detached or single-commit HEAD uses the bookmark color");
        }
    }

    private static void CheckFallbacks()
    {
        var root = new Commit { SHA = "root" };
        var head = Commit("head", root, "HEAD -> refs/heads/main");
        var baseline = CommitGraph.Generate([head, root], false, CommitGraphHighlighting.All, []);
        foreach (var bookmark in new[] { 0, -1, Bookmarks.Brushes.Length, int.MaxValue })
        {
            var graph = CommitGraph.Generate([head, root], false, CommitGraphHighlighting.All, [], bookmark);
            Check(graph.Dots.Select(x => x.Color).SequenceEqual(baseline.Dots.Select(x => x.Color)),
                "an unset or invalid bookmark retains the original graph colors");
        }
        head.Decorators.Clear();
        var noHead = CommitGraph.Generate([head, root], false, CommitGraphHighlighting.All, [], 2);
        Check(noHead.Dots.Select(x => x.Color).SequenceEqual(baseline.Dots.Select(x => x.Color)),
            "a history without HEAD retains the original graph colors");
        Check(CommitGraph.Generate([], false, CommitGraphHighlighting.All, [], 2).Dots.Count == 0,
            "an empty bookmarked history stays empty");

        head.ParseDecorators("HEAD -> refs/heads/main");
        var first = CommitGraph.Generate([head, root], false, CommitGraphHighlighting.All, [], 7);
        var firstColor = BrushColor(first.Dots[0].Color);
        CommitGraph.Generate([head, root], false, CommitGraphHighlighting.All, [], 1);
        Check(BrushColor(first.Dots[0].Color) == firstColor,
            "generating another repository's graph does not recolor an existing graph");
    }

    private static void CheckCustomPalettes()
    {
        try
        {
            foreach (var palette in new List<Color>[] { [Colors.AliceBlue], [Colors.Red], [Colors.Gold, Colors.Purple] })
            {
                CommitGraph.SetPens(palette, 3.5);
                var root = new Commit { SHA = "root" };
                var head = Commit("head", root, "HEAD -> refs/heads/main");
                var tip = Commit("tip", head, "refs/heads/feature");
                for (var bookmark = 1; bookmark < Bookmarks.Brushes.Length; bookmark++)
                {
                    var graph = CommitGraph.Generate([tip, head, root], false, CommitGraphHighlighting.All, [], bookmark);
                    Check(BrushColor(head.Color) == ((ISolidColorBrush)Bookmarks.Get(bookmark)).Color &&
                        graph.Dots.All(x => x.Color >= 0 && x.Color < CommitGraph.Pens.Count),
                        "custom and single-color palettes support every bookmark without invalid colors");
                    Check(CommitGraph.Pens[head.Color].Thickness == 3.5, "bookmark lines retain the configured graph thickness");
                }
            }
        }
        finally
        {
            CommitGraph.SetDefaultPens();
        }
    }

    private static void CheckStackedBookmarkColors()
    {
        try
        {
            foreach (var colors in new List<Color>[] { [Colors.Red, Colors.Orange], [Colors.Red, Colors.Orange, Colors.Gold] })
            {
                CommitGraph.SetPens(colors, 2);
                var root = new Commit { SHA = "root" };
                var head = Commit("head", root, "HEAD -> refs/heads/main");
                var branchBase = Commit("base", root, "refs/heads/base");
                var feature = Commit("feature", branchBase, "refs/heads/feature");
                var graph = CommitGraph.Generate([feature, branchBase, head, root], false, CommitGraphHighlighting.All, [], 1);
                Check(BrushColor(head.Color) == Colors.Red && feature.Color != head.Color && branchBase.Color != head.Color,
                    "stacked side branches preserve the color reserved for the current branch");
                Check(colors.Count == 2 || feature.Color != branchBase.Color,
                    "stacked side branches remain distinct when the palette has another available color");
                Check(graph.Dots.All(x => x.Color >= 0 && x.Color < CommitGraph.Pens.Count),
                    "small palettes terminate with valid colors when a bookmark color is reserved");
            }
        }
        finally
        {
            CommitGraph.SetDefaultPens();
        }
    }

    private static void CheckLiveBookmarks()
    {
        var fixture = Path.Combine(Path.GetTempPath(), "sourcegit-bookmark-colors-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        SourceGit.Native.OS.BasicDirectories.ConfigDir = fixture;
        SourceGit.Native.OS.BasicDirectories.CacheDir = fixture;
        try
        {
            App.BuildAvaloniaApp().SetupWithoutStarting();
            var launcher = new Launcher(null);
            typeof(App).GetField("_launcher", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Application.Current, launcher);
            var node = new RepositoryNode { Id = fixture, Name = "Fixture", IsRepository = true, Bookmark = 6 };
            Preferences.Instance.RepositoryNodes.Add(node);
            var repo = new Repository(false, fixture, Path.Combine(fixture, ".git"));
            Check(repo.Bookmark == node.Bookmark, "repositories read the saved bookmark before their tab is attached");
            var histories = new Histories(repo);
            typeof(Repository).GetField("_histories", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(repo, histories);
            launcher.Pages.Add(new LauncherPage(node, repo));
            var root = new Commit { SHA = "root" };
            var head = Commit("head", root, "HEAD -> refs/heads/main");
            histories.Commits = [head, root];
            var label = new SourceGit.Views.CommitRefsPresenter { DataContext = head, UseGraphColor = true, FontSize = 12 };
            label.Bind(SourceGit.Views.CommitRefsPresenter.GraphColorProperty, new Binding(nameof(SourceGit.Models.Commit.Color)));
            label.Measure(new Size(200, 26));
            Check(BrushColor(head.Color) == ((ISolidColorBrush)Bookmarks.Get(6)).Color,
                "the history view uses its repository tab's bookmark color");
            var original = histories.Graph;
            node.Bookmark = 7;
            Check(!ReferenceEquals(original, histories.Graph) && BrushColor(head.Color) == ((ISolidColorBrush)Bookmarks.Get(7)).Color,
                "editing a repository bookmark immediately regenerates its open graph");
            Check(!label.IsMeasureValid && label.GraphColor == head.Color,
                "editing a bookmark immediately invalidates the visible branch badge color");
            label.Measure(new Size(200, 26));
            var items = (List<SourceGit.Views.CommitRefsPresenter.RenderItem>)typeof(SourceGit.Views.CommitRefsPresenter)
                .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(label);
            Check(((ISolidColorBrush)items[0].Brush).Color == ((ISolidColorBrush)Bookmarks.Get(7)).Color,
                "the refreshed branch badge uses the new bookmark color");
            node.Bookmark = 0;
            Check(head.Color == 0, "clearing the bookmark restores the default graph color");
            node.IsUnmanaged = true;
            Preferences.Instance.RepositoryNodes.Remove(node);
            node.Bookmark = 3;
            Check(repo.Bookmark == 3 && BrushColor(head.Color) == ((ISolidColorBrush)Bookmarks.Get(3)).Color,
                "unmanaged tabs use their own bookmark even without a saved repository node");
        }
        finally
        {
            var root = Path.GetFullPath(Path.GetTempPath());
            if (!Path.GetFullPath(fixture).StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Fixture must stay inside the temporary directory");
            Directory.Delete(fixture, true);
        }
    }

    private static Color BrushColor(int color) => ((ISolidColorBrush)CommitGraph.Pens[color].Brush).Color;

    private static Commit Commit(string sha, Commit parent, string decorators = "")
    {
        var commit = new Commit { SHA = sha, Parents = [parent.SHA] };
        commit.ParseDecorators(decorators);
        return commit;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception("FAIL: " + message);
        _checks++;
    }
}
