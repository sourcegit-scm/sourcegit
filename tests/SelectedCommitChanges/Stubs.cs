using System.Diagnostics;
using System.Text;

// Only the app infrastructure is replaced. The query, parser, zipper, and change
// types are linked directly from production and execute against real Git repos.
namespace SourceGit.Commands
{
    public class Command
    {
        public enum EditorType { None }
        public string WorkingDirectory { get; set; }
        public string Context { get; set; }
        public string Args { get; set; }
        public EditorType Editor { get; set; }
        public static Func<ProcessStartInfo> StartInfoOverride { get; set; }
        public static int StartInfoCount { get; set; }

        protected ProcessStartInfo CreateGitStartInfo(bool redirect)
        {
            StartInfoCount++;
            return StartInfoOverride?.Invoke() ?? new ProcessStartInfo("git", "--no-pager -c core.quotepath=off " + Args)
            {
                WorkingDirectory = WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = redirect,
                RedirectStandardError = redirect,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
        }
    }
}

namespace SourceGit.Models
{
    public class Commit
    {
        public string SHA { get; set; }
        public List<string> Parents { get; set; } = [];
        public string FirstParentToCompare => Parents.Count > 0 ? $"{SHA}^" : "4b825dc642cb6eb9a060e54bf8d69288fbee4904";
    }
}

namespace SourceGit
{
    public static class StringExtensions
    {
        public static string Quoted(this string value) => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
namespace SourceGit.Models
{
    public class FileVersion
    {
        public string SHA { get; set; }
        public bool HasParent { get; set; }
        public string Path { get; set; }
        public string OriginalPath { get; set; }
        public Change Change { get; set; }
    }
}

namespace CommunityToolkit.Mvvm.ComponentModel
{
    public class ObservableObject
    {
        protected bool SetProperty<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            return true;
        }
        protected void OnPropertyChanged(string name) { }
    }
}
namespace Avalonia.Threading
{
    public sealed class Dispatcher
    {
        public static Dispatcher UIThread { get; } = new();
        private readonly System.Collections.Concurrent.ConcurrentQueue<(Action Action, TaskCompletionSource Completion)> _pending = new();
        public bool Hold { get; set; }
        public int PendingCount => _pending.Count;
        public Task InvokeAsync(Action action)
        {
            if (!Hold)
            { action(); return Task.CompletedTask; }
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending.Enqueue((action, completion));
            return completion.Task;
        }
        public void Drain()
        {
            while (_pending.TryDequeue(out var pending))
            {
                try
                { pending.Action(); pending.Completion.SetResult(); }
                catch (Exception ex) { pending.Completion.SetException(ex); }
            }
        }
    }
}
namespace SourceGit.Commands
{
    public class DiffTool
    {
        public DiffTool(string repo, Models.DiffOption option) { }
        public void Open() { }
    }
}
namespace SourceGit.ViewModels
{
    public class Repository { public string FullPath { get; set; } }
    // RevisionFiles owns this real app type. This focused harness checks the
    // summary VM's ownership/lifecycle without bootstrapping Avalonia.
    public class CommitDetailSharedData { public int ActiveTabIndex { get; set; } }
    public class CommitDetail : IDisposable
    {
        public Models.Commit Commit { get; set; }
        public int ActiveTabIndex { get; set; }
        public bool IsDisposed { get; private set; }
        public bool DefersInformationLoading { get; }
        public SemaphoreSlim InformationLoadGate { get; }
        public CommitDetail(Repository repo, CommitDetailSharedData sharedData, bool deferInformationLoading = false, SemaphoreSlim informationLoadGate = null)
        {
            DefersInformationLoading = deferInformationLoading;
            InformationLoadGate = informationLoadGate;
            ActiveTabIndex = sharedData?.ActiveTabIndex ?? 0;
        }
        public void Dispose() => IsDisposed = true;
    }
    public class ChangeSelection
    {
        public List<Models.Change> Changes { get; }
        public int Count => Changes.Count;
        public bool HasFolder { get; set; }
        public ChangeSelection(List<Models.Change> changes) { Changes = changes ?? []; }
    }
    public class DiffContext
    {
        public Models.DiffOption Option { get; }
        public DiffContext(string repo, Models.DiffOption option) { Option = option; }
    }
}
