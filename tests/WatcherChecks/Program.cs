using System.Reflection;
using SourceGit.Models;

var root = Path.Combine(Path.GetTempPath(), $"sourcegit-watcher-{Guid.NewGuid():N}");
Directory.CreateDirectory(Path.Combine(root, ".git"));
try
{
    var repo = new TestRepository();
    using var watcher = new Watcher(repo, root, Path.Combine(root, ".git"));
    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
    var timer = (Timer)typeof(Watcher).GetField("_timer", flags).GetValue(watcher);
    timer.Change(Timeout.Infinite, Timeout.Infinite);

    void Invoke(string method, params object[] args) => typeof(Watcher).GetMethod(method, flags).Invoke(watcher, args);
    void Tick() => Invoke("Tick", new object[] { null });
    void WorkingCopy() => Invoke("HandleWorkingCopyFileChanged", "output.dll", Path.Combine(root, "output.dll"));
    void Git(string path) => Invoke("HandleGitDirFileChanged", path);
    void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    SourceGit.ViewModels.Preferences.Instance.RefreshThrottling = 200;
    Git("HEAD");
    Git("refs/tags/v1");
    Git("refs/stash");
    for (var i = 0; i < 6; i++)
    {
        WorkingCopy();
        Thread.Sleep(80);
        Tick();
        Check(repo.Calls.Count == 0, "Continuous working-copy changes must delay every pending refresh.");
    }

    Thread.Sleep(160);
    Tick();
    Check(repo.Calls.SequenceEqual(new[] { "branches", "worktrees", "working-copy", "submodules", "stashes", "tags", "commits" }),
        "A quiet period must refresh all pending categories once.");
    repo.Calls.Clear();
    Tick();
    Check(repo.Calls.Count == 0, "Pending refreshes must be consumed.");

    WorkingCopy();
    Thread.Sleep(240);
    Invoke("HandleWorkingCopyFileChanged", ".vs/cache", Path.Combine(root, ".vs", "cache"));
    Git("index.lock");
    Tick();
    Check(repo.Calls.SequenceEqual(new[] { "working-copy" }), "Ignored paths must not restart the quiet period.");
    repo.Calls.Clear();

    SourceGit.ViewModels.Preferences.Instance.RefreshThrottling = 5000;
    WorkingCopy();
    Tick();
    Check(repo.Calls.Count == 0, "The maximum setting must delay refreshes.");
    typeof(Watcher).GetField("_lastChange", flags).SetValue(watcher, DateTime.Now.AddMilliseconds(-5100).ToFileTime());
    Tick();
    Check(repo.Calls.SequenceEqual(new[] { "working-copy" }), "Refresh must resume after the maximum quiet period.");
    repo.Calls.Clear();

    WorkingCopy();
    SourceGit.ViewModels.Preferences.Instance.RefreshThrottling = 0;
    Tick();
    Check(repo.Calls.SequenceEqual(new[] { "working-copy" }), "Changing the setting to zero must release pending refreshes immediately.");
    repo.Calls.Clear();

    using (watcher.Lock())
    {
        WorkingCopy();
        Tick();
        Check(repo.Calls.Count == 0, "Watcher locks must still suppress refreshes.");
    }
    Tick();
    Check(repo.Calls.SequenceEqual(new[] { "working-copy" }), "Unlocking must preserve pending refreshes.");
    repo.Calls.Clear();

    SourceGit.ViewModels.Preferences.Instance.RefreshThrottling = 200;
    Invoke("OnWatcherError", null, new ErrorEventArgs(new InternalBufferOverflowException()));
    Tick();
    Check(repo.Calls.Count == 0, "Watcher overflow recovery must respect the quiet period.");
    Thread.Sleep(240);
    Tick();
    Check(repo.Calls.Count == 7, "Watcher overflow recovery must reload all categories.");
    repo.Calls.Clear();

    var preferences = SourceGit.ViewModels.Preferences.Instance;
    var filesystemWatchers = (List<FileSystemWatcher>)typeof(Watcher).GetField("_watchers", flags).GetValue(watcher);
    WorkingCopy();
    preferences.ManualRefresh = true;
    Check(filesystemWatchers.All(x => !x.EnableRaisingEvents), "Manual mode must disable filesystem hooks immediately.");
    preferences.RefreshThrottling = 0;
    WorkingCopy();
    Git("HEAD");
    Invoke("OnWatcherError", null, new ErrorEventArgs(new InternalBufferOverflowException()));
    Tick();
    Check(repo.Calls.Count == 0, "Manual mode must suppress queued changes and overflow recovery, even with zero throttling.");

    using (var manualWatcher = new Watcher(repo, root, Path.Combine(root, ".git")))
    {
        var manualFilesystemWatchers = (List<FileSystemWatcher>)typeof(Watcher).GetField("_watchers", flags).GetValue(manualWatcher);
        // Run the startup action explicitly as well, to check that it cannot enable hooks in manual mode.
        typeof(Watcher).GetMethod("UpdateWatching", flags).Invoke(manualWatcher, null);
        Check(manualFilesystemWatchers.All(x => !x.EnableRaisingEvents), "Repositories opened in manual mode must leave hooks disabled.");
    }

    preferences.ManualRefresh = false;
    Check(filesystemWatchers.All(x => x.EnableRaisingEvents), "Returning to automatic mode must re-enable filesystem hooks.");
    Thread.Sleep(1);
    Tick();
    Check(repo.Calls.Count == 7, "Returning to automatic mode must reload changes missed while hooks were disabled.");
    repo.Calls.Clear();
    Tick();
    Check(repo.Calls.Count == 0, "Automatic recovery must refresh only once.");
    Console.WriteLine("Watcher checks passed.");
}
finally
{
    Directory.Delete(root, true);
}

sealed class TestRepository : IRepository
{
    public List<string> Calls { get; } = new();
    public bool MayHaveSubmodules() => true;
    public void RefreshBranches() => Calls.Add("branches");
    public void RefreshWorktrees() => Calls.Add("worktrees");
    public void RefreshTags() => Calls.Add("tags");
    public void RefreshCommits() => Calls.Add("commits");
    public void RefreshSubmodules() => Calls.Add("submodules");
    public void RefreshWorkingCopyChanges() => Calls.Add("working-copy");
    public void RefreshStashes() => Calls.Add("stashes");
}

namespace SourceGit.ViewModels
{
    // Keep these watcher checks independent of the desktop application and user settings.
    public sealed class Preferences : System.ComponentModel.INotifyPropertyChanged
    {
        public static Preferences Instance { get; } = new();
        public int RefreshThrottling { get; set; } = 1000;
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        public bool ManualRefresh
        {
            get => _manualRefresh;
            set
            {
                if (_manualRefresh == value)
                    return;

                _manualRefresh = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ManualRefresh)));
            }
        }

        private bool _manualRefresh;
    }
}
