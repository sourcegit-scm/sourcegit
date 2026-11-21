using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SourceGit;
using SourceGit.ViewModels;
using Views = SourceGit.Views;

internal static class Program
{
    private static int _checks;

    [STAThread]
    private static void Main(string[] args)
    {
        var fixture = Path.Combine(Path.GetTempPath(), "sourcegit-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        SourceGit.Native.OS.BasicDirectories.ConfigDir = fixture;
        SourceGit.Native.OS.BasicDirectories.CacheDir = fixture;
        Repository repo = null;
        Window window = null;
        try
        {
            App.BuildAvaloniaApp().SetupWithoutStarting();
            App.SetLocale("en_US");
            Git(fixture, "init -b main");
            Git(fixture, "config user.name Test");
            Git(fixture, "config user.email test@example.com");
            Git(fixture, "commit --allow-empty -m initial");
            repo = new Repository(false, fixture, Path.Combine(fixture, ".git"));
            repo.Open();
            var pref = Preferences.Instance;
            pref.SetCanModify();
            pref.UseWideDetailsLayoutInHistories = true;
            var view = new Views.Repository { DataContext = repo };
            window = new Window { Title = "SourceGit Layout Checks", Width = 1100, Height = 800, Content = view };
            window.Show();
            PumpUntil(() => !repo.Histories.IsLoading && repo.Histories.Commits.Count == 1);
            repo.Histories.SelectedCommits = [repo.Histories.Commits[0]];
            PumpUntil(() => repo.Histories.DetailContext is CommitDetail);
            CheckLayouts(repo, view, window, args.Contains("--render"));
            Console.WriteLine($"PASS: {_checks} layout checks");
        }
        finally
        {
            window?.Close();
            repo?.Close();
            if (!Path.GetFullPath(fixture).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Fixture must stay inside the temporary directory");
            foreach (var file in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(fixture, true);
        }
    }

    private static void CheckLayouts(Repository repo, Views.Repository view, Window window, bool render)
    {
        var pref = Preferences.Instance;
        var histories = view.FindControl<Views.Histories>("DashboardHistories");
        var layout = histories.GetVisualDescendants().OfType<Views.HistoriesLayout>().Single();
        var historyArea = histories.FindControl<Grid>("HistoryArea");
        var sidebar = view.FindControl<Grid>("Sidebar");
        var mainBody = view.FindControl<Grid>("MainBody");
        var details = layout.Children[2];
        var list = histories.FindControl<Views.HistoriesCommitList>("CommitListContainer");
        var splitter = view.FindControl<GridSplitter>("SidebarSplitter");

        foreach (var width in new[] { 1100, 800 })
        {
            window.Width = width;
            pref.UseWideDetailsLayoutInHistories = true;
            Arrange(window);
            Check(ReferenceEquals(sidebar.Parent, historyArea), "startup/switch to Wide Details moves sidebar beside history");
            Check(ReferenceEquals(sidebar.DataContext, repo), "sidebar retains repository context");
            Check(ReferenceEquals(splitter.Parent, historyArea), "sidebar splitter moves with sidebar");
            Check(Near(details.Bounds.Width, mainBody.Bounds.Width), "Wide Details fills entire main body width");
            Check(Near(sidebar.TranslatePoint(default, view)!.Value.Y + sidebar.Bounds.Height,
                details.TranslatePoint(default, view)!.Value.Y - 3), "sidebar ends above details splitter");
            Check(list.Bounds.Width < details.Bounds.Width - 100, "sidebar reduces history width only");

            var detailsWidth = details.Bounds.Width;
            var historyWidth = list.Bounds.Width;
            var sidebarWidth = view.SidebarWidth;
            historyArea.ColumnDefinitions[0].Width = new GridLength(sidebarWidth.Value + 40);
            Arrange(window);
            Check(Near(view.SidebarWidth.Value, sidebarWidth.Value + 40), "resizing sidebar updates bound width");
            Check(Near(details.Bounds.Width, detailsWidth) && Near(list.Bounds.Width, historyWidth - 40), "sidebar resizing leaves details unchanged");
            view.SidebarWidth = sidebarWidth;

            view.IsNormalSidebarVisible = false;
            Arrange(window);
            Check(Near(list.Bounds.Width, details.Bounds.Width), "collapsed sidebar returns all width to history");
            view.IsCommitSearchPanelVisible = true;
            Arrange(window);
            Check(view.FindControl<Views.CommitSearchView>("CommitSearchPanel").Bounds.Width >= 200, "commit search uses upper sidebar");
            CheckCommitSearch(repo, view, window, true);
            Check(Near(details.Bounds.Width, detailsWidth), "commit search leaves full width details");
            view.IsCommitSearchPanelVisible = false;
            view.IsNormalSidebarVisible = true;

            repo.Histories.IsCollapseDetails = true;
            Arrange(window);
            Check(Near(details.Bounds.Height, 28) && Near(details.Bounds.Width, mainBody.Bounds.Width), "Wide Details collapses to a full width tab bar");
            repo.Histories.IsCollapseDetails = false;

            foreach (var page in new[] { 1, 2, 0 })
            {
                repo.SelectedViewIndex = page;
                Arrange(window);
                Check(ReferenceEquals(sidebar.Parent, page == 0 ? historyArea : mainBody), "sidebar follows page selection");
                CheckCommitSearch(repo, view, window);
            }

            if (render)
            {
                using var bitmap = new RenderTargetBitmap(new PixelSize(width, 800));
                bitmap.Render(window);
                bitmap.Save(Path.Combine(AppContext.BaseDirectory, $"wide-details-{width}.png"));
            }

            pref.UseTwoColumnsLayoutInHistories = true;
            Arrange(window);
            Check(!pref.UseWideDetailsLayoutInHistories && ReferenceEquals(sidebar.Parent, mainBody), "Horizontal restores outer sidebar");
            Check(Near(details.Bounds.Height, layout.Bounds.Height), "Horizontal details are beside history");
            CheckCommitSearch(repo, view, window);
            pref.UseTwoColumnsLayoutInHistories = false;
            Arrange(window);
            Check(ReferenceEquals(sidebar.Parent, mainBody) && details.Bounds.Width < mainBody.Bounds.Width - 100, "Vertical preserves original sidebar arrangement");
            CheckCommitSearch(repo, view, window);
        }

        pref.UseWideDetailsLayoutInHistories = true;
        CheckAdvancedOptions(histories, window);
        pref.Save();
        using var saved = JsonDocument.Parse(File.ReadAllText(Path.Combine(SourceGit.Native.OS.BasicDirectories.ConfigDir, "preference.json")));
        Check(saved.RootElement.GetProperty("UseWideDetailsLayoutInHistories").GetBoolean(), "Wide Details preference is persisted");
    }

    private static void CheckCommitSearch(Repository repo, Views.Repository view, Window window, bool runQuery = false)
    {
        var search = view.FindControl<Views.CommitSearchView>("CommitSearchPanel");
        var context = repo.Histories.SearchCommitContext;
        Check(ReferenceEquals(search.DataContext, context), "commit search retains its context after reparenting");
        var spinner = search.GetVisualDescendants().OfType<Views.LoadingIcon>().Single();
        Check(!spinner.IsVisible && !context.IsQuerying, "idle commit search hides spinner before any query");

        if (!runQuery)
            return;

        repo.Histories.IsSearchingCommits = true;
        var input = search.FindControl<TextBox>("TxtSearchCommitsBox");
        input.Text = "initial";
        Arrange(window);
        Check(context.Filter == "initial", "search input updates the search context");
        context.StartSearch();
        Check(spinner.IsVisible, "active commit search shows spinner");
        PumpUntil(() => !context.IsQuerying);
        Check(!spinner.IsVisible && context.Results?.Count == 1, "commit search completes with a result and hides spinner");
        context.ClearFilter();
        repo.Histories.IsSearchingCommits = false;
        Arrange(window);
        Check(string.IsNullOrEmpty(input.Text) && !spinner.IsVisible, "clearing and closing commit search resets its controls");
    }

    private static void CheckAdvancedOptions(Views.Histories histories, Window window)
    {
        var pref = Preferences.Instance;
        var button = histories.GetVisualDescendants().OfType<Button>()
            .Single(x => x.GetValue(ToolTip.TipProperty) as string == App.Text("Histories.AdvancedOptions"));
        ContextMenu opened = null;
        using var subscription = MenuBase.OpenedEvent.AddClassHandler<ContextMenu>((menu, _) => opened = menu);
        var openMenu = typeof(Views.Histories).GetMethod("OnOpenAdvancedOptions", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var name in new[] { "Vertical", "Horizontal", "WideDetails" })
        {
            openMenu.Invoke(histories, [button, new RoutedEventArgs()]);
            PumpUntil(() => opened?.IsOpen == true);
            var item = opened.Items.OfType<MenuItem>().Single(x => x.Header as string == App.Text("Repository.HistoriesLayout." + name));
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            opened.Close();
            Arrange(window);
            Check(pref.UseTwoColumnsLayoutInHistories == (name == "Horizontal") && pref.UseWideDetailsLayoutInHistories == (name == "WideDetails"), "Advanced options selects " + name);
            openMenu.Invoke(histories, [button, new RoutedEventArgs()]);
            PumpUntil(() => opened?.IsOpen == true);
            var layouts = opened.Items.OfType<MenuItem>().Skip(1).Take(3).ToList();
            Check(layouts.Count(x => x.Icon != null) == 1 && layouts.Single(x => x.Icon != null).Header as string == App.Text("Repository.HistoriesLayout." + name), "only selected layout has a checkmark");
            opened.Close();
        }
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 1;

    private static void Arrange(Window window)
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        Dispatcher.UIThread.RunJobs();
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
