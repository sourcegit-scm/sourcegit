using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SourceGit.Models;
using SourceGit.ViewModels;

var fixture = Path.Combine(Path.GetTempPath(), "sourcegit-navigation-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixture);
SourceGit.Native.OS.GitExecutable = "git";
SourceGit.Native.OS.BasicDirectories.ConfigDir = fixture;
SourceGit.Native.OS.BasicDirectories.CacheDir = fixture;
typeof(Preferences).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, new Preferences());
AppBuilder.Configure<SourceGit.App>().UseSkia().WithInterFont()
    .ConfigureFonts(manager => manager.AddFontCollection(new Avalonia.Media.Fonts.EmbeddedFontCollection(
        new Uri("fonts:SourceGit"), new Uri("avares://SourceGit/Resources/Fonts"))))
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();

Git("init", "-b", "main");
Git("config", "user.name", "Navigation Checks");
Git("config", "user.email", "navigation@example.com");
for (var i = 0; i < 3; i++)
    Git("commit", "--allow-empty", "-m", $"Commit {i}");
var commits = new SourceGit.Commands.QueryCommits(fixture, "HEAD").GetResultAsync().GetAwaiter().GetResult();
var head = commits[0];
var second = commits[1];
var oldest = commits[2];
// Put HEAD below the top row to distinguish Home from scrolling to the first row.
commits.InsertRange(0, Enumerable.Range(1, 60).Select(i => new Commit { SHA = i.ToString("x40"), Subject = $"Row {i}" }));
var repo = new Repository(false, fixture, Path.Combine(fixture, ".git"));
var vm = new Histories(repo) { Commits = commits, IsLoading = false };
var view = new SourceGit.Views.Histories { DataContext = vm };
var window = new Window { Content = view, Width = 1000, Height = 650 };
window.Show();
Pump(200);
var grid = view.FindControl<SourceGit.Views.HistoriesCommitList>("CommitListContainer")!;
var homeButton = view.FindControl<Button>("NavigationHomeButton")!;
var backButton = view.FindControl<Button>("NavigationBackButton")!;
var forwardButton = view.FindControl<Button>("NavigationForwardButton")!;

Check(!vm.CanNavigateBack && !vm.CanNavigateForward, "empty stack");
Check(!backButton.IsEnabled && !forwardButton.IsEnabled, "empty stack disables toolbar navigation");
vm.SelectedCommits = [oldest];
Pump(1150);
vm.SelectedCommits = [second];
Pump(150);
vm.SelectedCommits = [head];
Pump(1150);
Run(vm.NavigateBackAsync());
Check(vm.SelectedCommits.Single() == oldest, "rapid intermediate selection is omitted");
Check(vm.CanNavigateForward, "Back enables Forward");
Check(forwardButton.IsEnabled && forwardButton.Command != null, "Forward toolbar command is bound");
Check(IsVisible(oldest), "Back brings destination into view");
Run(vm.NavigateForwardAsync());
Check(vm.SelectedCommits.Single() == head, "Forward restores destination");
Pump(1150);
Run(vm.NavigateBackAsync());
Check(vm.SelectedCommits.Single() == oldest, "waiting after stack navigation does not append visits");
vm.SelectedCommits = [second];
Pump(1150);
Check(!vm.CanNavigateForward, "new visit truncates forward history");
vm.SelectedCommits = [head];
Pump(100);
Run(vm.NavigateBackAsync());
Check(vm.SelectedCommits.Single() == second, "Back from an unsettled selection returns to last stable visit");

grid.ScrollIntoView(commits[0], null);
Pump(100);
homeButton.Command!.Execute(null);
Pump(300);
Check(vm.SelectedCommits.Single() == head && IsVisible(head), "Home selects and reveals actual HEAD below top");
grid.ScrollIntoView(commits[0], null);
Pump(100);
Run(vm.NavigateHomeAsync());
Check(IsVisible(head), "Home reveals an already selected HEAD after manual scrolling");

grid.Focus();
grid.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home, KeyModifiers = KeyModifiers.Alt });
Pump(300);
Check(vm.SelectedCommits.Single() == head, "Alt+Home");
Pump(1150);
grid.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Left, KeyModifiers = KeyModifiers.Alt });
Check(vm.SelectedCommits.Single() == second, "Alt+Left");
grid.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right, KeyModifiers = KeyModifiers.Alt });
Check(vm.SelectedCommits.Single() == head, "Alt+Right");

// Restore a destination excluded by a subsequent history refresh.
vm.Commits = [head];
Run(vm.NavigateBackAsync());
Check(vm.SelectedCommits.Single().SHA == second.SHA && vm.Commits.Any(c => c.SHA == second.SHA), "hidden destination restored as a selectable row");
Check(vm.Commits.IndexOf(head) < vm.Commits.FindIndex(c => c.SHA == second.SHA), "restored parent follows its child in graph order");
Check(IsVisible(vm.SelectedCommits.Single()), "restored destination visible");
vm.CancelNavigationVisit();
vm.Commits = [second];
Run(vm.NavigateHomeAsync());
Check(vm.SelectedCommits.Single().SHA == head.SHA && IsVisible(vm.SelectedCommits.Single()), "Home restores worktree HEAD omitted from loaded history");
vm.Commits = [];
Run(vm.NavigateHomeAsync());
Check(vm.SelectedCommits.Single().SHA == head.SHA, "Home resolves worktree HEAD even with empty loaded history");

var linked = Path.Combine(fixture, "linked-worktree");
Git("worktree", "add", "--detach", linked, oldest.SHA);
File.WriteAllText(Path.Combine(linked, "local.txt"), "Uncommitted work in the linked worktree");
var linkedGitDir = Git("-C", linked, "rev-parse", "--absolute-git-dir").Trim();
var linkedVm = new Histories(new Repository(false, linked, linkedGitDir)) { Commits = [head, second, oldest], IsLoading = false };
view.DataContext = linkedVm;
Pump(100);
Run(linkedVm.NavigateHomeAsync());
Check(linkedVm.SelectedCommits.Single().SHA == oldest.SHA && IsVisible(linkedVm.SelectedCommits.Single()), "Home uses linked worktree's detached HEAD despite another HEAD decoration in loaded history");
linkedVm.Commits = [head];
Run(linkedVm.NavigateHomeAsync());
Check(linkedVm.SelectedCommits.Single().SHA == oldest.SHA && IsVisible(linkedVm.SelectedCommits.Single()), "Home restores the linked worktree's local changes base when omitted");

var unborn = Path.Combine(fixture, "unborn");
Git("init", unborn);
var unbornVm = new Histories(new Repository(false, unborn, Path.Combine(unborn, ".git"))) { Commits = [second] };
Run(unbornVm.NavigateHomeAsync());
Check(unbornVm.SelectedCommits.Single() == second, "Home falls back to first row when worktree has no HEAD commit");
unbornVm.Commits = [];
Run(unbornVm.NavigateHomeAsync());
Check(unbornVm.SelectedCommits.Count == 0, "Home tolerates an unborn worktree and empty history");
unbornVm.CancelNavigationVisit();

var missing = new Commit { SHA = new string('f', 40), Subject = "Pruned commit" };
var reviewVm = new Histories(repo) { Commits = [head, missing, oldest], IsLoading = false };
view.DataContext = reviewVm;
reviewVm.SelectedCommits = [oldest];
Pump(1150);
reviewVm.SelectedCommits = [missing];
Pump(1150);
reviewVm.SelectedCommits = [head];
Pump(1150);
reviewVm.Commits = [head, oldest];
Run(reviewVm.NavigateBackAsync());
Check(reviewVm.SelectedCommits.Single() == oldest, "Back skips a pruned commit instead of getting stuck");
Run(reviewVm.NavigateForwardAsync());
Check(reviewVm.SelectedCommits.Single() == head, "Forward works after removing an unavailable visit");

reviewVm.Commits = [head, second];
var pendingBack = reviewVm.NavigateBackAsync();
reviewVm.SelectedCommits = [second];
Run(pendingBack);
Check(reviewVm.SelectedCommits.Single() == second, "new selection cancels pending commit lookup");
Run(reviewVm.NavigateBackAsync());
Check(reviewVm.SelectedCommits.Single() == head, "cancelled navigation leaves stack position unchanged");

window.Content = null;
reviewVm.SelectedCommits = [second];
Pump(1150);
window.Content = view;
Pump(100);
Run(reviewVm.NavigateBackAsync());
Check(reviewVm.SelectedCommits.Single() == head && !reviewVm.CanNavigateForward, "hidden view does not record visits and resumes on return");
window.Close();
Console.WriteLine("History navigation checks passed.");

void Check(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
    Console.WriteLine($"PASS: {message}");
}

void Pump(int milliseconds)
{
    using var cancellation = new CancellationTokenSource(milliseconds);
    Dispatcher.UIThread.MainLoop(cancellation.Token);
}

void Run(Task task)
{
    var timer = Stopwatch.StartNew();
    while (!task.IsCompleted && timer.ElapsedMilliseconds < 10000)
        Pump(10);
    if (!task.IsCompleted)
        throw new Exception("Navigation timed out");
    task.GetAwaiter().GetResult();
    Pump(100);
}

bool IsVisible(Commit commit)
{
    var presenter = grid.GetVisualDescendants().OfType<DataGridRowsPresenter>().First();
    return presenter.GetVisualDescendants().OfType<DataGridRow>().Any(row =>
    {
        var position = row.TranslatePoint(default, presenter);
        return row.DataContext == commit && row.IsVisible && position.HasValue &&
            position.Value.Y >= -0.5 && position.Value.Y + row.Bounds.Height <= presenter.Bounds.Height + 0.5;
    });
}

string Git(params string[] arguments)
{
    var start = new ProcessStartInfo("git") { WorkingDirectory = fixture, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in arguments)
        start.ArgumentList.Add(argument);
    using var process = Process.Start(start)!;
    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
        throw new Exception(error);
    return output;
}
