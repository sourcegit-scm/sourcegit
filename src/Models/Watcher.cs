using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SourceGit.Models
{
    public class Watcher : IDisposable
    {
        public Watcher(IRepository repo, string fullpath, string gitDir)
        {
            _repo = repo;

            var testGitDir = new DirectoryInfo(Path.Combine(fullpath, ".git")).FullName;
            var desiredDir = new DirectoryInfo(gitDir).FullName;
            if (testGitDir.Equals(desiredDir, StringComparison.Ordinal))
            {
                var combined = new FileSystemWatcher();
                combined.Path = fullpath;
                combined.Filter = "*";
                combined.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime;
                combined.IncludeSubdirectories = true;
                combined.Created += OnRepositoryChanged;
                combined.Renamed += OnRepositoryChanged;
                combined.Changed += OnRepositoryChanged;
                combined.Deleted += OnRepositoryChanged;
                combined.EnableRaisingEvents = true;

                _watchers.Add(combined);
            }
            else
            {
                var wc = new FileSystemWatcher();
                wc.Path = fullpath;
                wc.Filter = "*";
                wc.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime;
                wc.IncludeSubdirectories = true;
                wc.Created += OnWorkingCopyChanged;
                wc.Renamed += OnWorkingCopyChanged;
                wc.Changed += OnWorkingCopyChanged;
                wc.Deleted += OnWorkingCopyChanged;
                wc.EnableRaisingEvents = true;

                var git = new FileSystemWatcher();
                git.Path = gitDir;
                git.Filter = "*";
                git.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.FileName;
                git.IncludeSubdirectories = true;
                git.Created += OnGitDirChanged;
                git.Renamed += OnGitDirChanged;
                git.Changed += OnGitDirChanged;
                git.Deleted += OnGitDirChanged;
                git.EnableRaisingEvents = true;

                _watchers.Add(wc);
                _watchers.Add(git);
            }

            _timer = new Timer(Tick, null, 100, 100);
        }

        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                if (_lockCount > 0)
                    _lockCount--;
            }
            else
            {
                _lockCount++;
            }
        }

        public void SetSubmodules(List<Submodule> submodules)
        {
            lock (_lockSubmodule)
            {
                _submodules.Clear();
                foreach (var submodule in submodules)
                    _submodules.Add(submodule.Path);
            }
        }

        public void MarkBranchUpdated()
        {
            _updateBranch = 0;
        }

        public void MarkTagUpdated()
        {
            _updateTags = 0;
        }

        public void MarkWorkingCopyUpdated()
        {
            _updateWC = 0;
        }

        public void Dispose()
        {
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _watchers.Clear();
            _timer.Dispose();
            _timer = null;
        }

        private void Tick(object sender)
        {
            if (_lockCount > 0)
                return;

            var now = DateTime.Now.ToFileTime();
            if (_updateBranch > 0 && now > _updateBranch)
            {
                _updateBranch = 0;
                _updateWC = 0;

                if (_updateTags > 0)
                {
                    _updateTags = 0;
                    _repo.RefreshTags();
                }

                if (_updateSubmodules > 0 || _repo.MayHaveSubmodules())
                {
                    _updateSubmodules = 0;
                    _repo.RefreshSubmodules();
                }

                _repo.RefreshBranches();
                _repo.RefreshCommits();
                _repo.RefreshWorkingCopyChanges();
                _repo.RefreshWorktrees();
            }

            if (_updateWC > 0 && now > _updateWC)
            {
                _updateWC = 0;
                _repo.RefreshWorkingCopyChanges();
            }

            if (_updateSubmodules > 0 && now > _updateSubmodules)
            {
                _updateSubmodules = 0;
                _repo.RefreshSubmodules();
            }

            if (_updateStashes > 0 && now > _updateStashes)
            {
                _updateStashes = 0;
                _repo.RefreshStashes();
            }

            if (_updateTags > 0 && now > _updateTags)
            {
                _updateTags = 0;
                _repo.RefreshTags();
                _repo.RefreshCommits();
            }
        }

        private void OnRepositoryChanged(object o, FileSystemEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Name) || e.Name.Equals(".git", StringComparison.Ordinal))
                return;

            var name = e.Name.Replace('\\', '/').TrimEnd('/');
            if (name.EndsWith("/.git", StringComparison.Ordinal))
                return;

            if (name.StartsWith(".git/", StringComparison.Ordinal))
                HandleGitDirFileChanged(name.Substring(5));
            else
                HandleWorkingCopyFileChanged(name);
        }

        private void OnGitDirChanged(object o, FileSystemEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Name))
                return;

            var name = e.Name.Replace('\\', '/').TrimEnd('/');
            HandleGitDirFileChanged(name);
        }

        private void OnWorkingCopyChanged(object o, FileSystemEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Name))
                return;

            var name = e.Name.Replace('\\', '/').TrimEnd('/');
            if (name.Equals(".git", StringComparison.Ordinal) ||
                name.StartsWith(".git/", StringComparison.Ordinal) ||
                name.EndsWith("/.git", StringComparison.Ordinal))
                return;

            HandleWorkingCopyFileChanged(name);
        }

        private void HandleGitDirFileChanged(string name)
        {
            if (name.Contains("fsmonitor--daemon/", StringComparison.Ordinal) ||
                name.EndsWith(".lock", StringComparison.Ordinal) ||
                name.StartsWith("lfs/", StringComparison.Ordinal))
                return;

            if (name.StartsWith("modules", StringComparison.Ordinal))
            {
                if (name.EndsWith("/HEAD", StringComparison.Ordinal) ||
                    name.EndsWith("/ORIG_HEAD", StringComparison.Ordinal))
                {
                    _updateSubmodules = DateTime.Now.AddSeconds(1).ToFileTime();
                    _updateWC = DateTime.Now.AddSeconds(1).ToFileTime();
                }
            }
            else if (name.Equals("MERGE_HEAD", StringComparison.Ordinal) ||
                name.Equals("AUTO_MERGE", StringComparison.Ordinal))
            {
                if (_repo.MayHaveSubmodules())
                    _updateSubmodules = DateTime.Now.AddSeconds(1).ToFileTime();
            }
            else if (name.StartsWith("refs/tags", StringComparison.Ordinal))
            {
                _updateTags = DateTime.Now.AddSeconds(.5).ToFileTime();
            }
            else if (name.StartsWith("refs/stash", StringComparison.Ordinal))
            {
                _updateStashes = DateTime.Now.AddSeconds(.5).ToFileTime();
            }
            else if (name.Equals("HEAD", StringComparison.Ordinal) ||
                name.Equals("BISECT_START", StringComparison.Ordinal) ||
                name.StartsWith("refs/heads/", StringComparison.Ordinal) ||
                name.StartsWith("refs/remotes/", StringComparison.Ordinal) ||
                (name.StartsWith("worktrees/", StringComparison.Ordinal) && name.EndsWith("/HEAD", StringComparison.Ordinal)))
            {
                _updateBranch = DateTime.Now.AddSeconds(.5).ToFileTime();
            }
            else if (name.StartsWith("objects/", StringComparison.Ordinal) || name.Equals("index", StringComparison.Ordinal))
            {
                _updateWC = DateTime.Now.AddSeconds(1).ToFileTime();
            }
        }

        private void HandleWorkingCopyFileChanged(string name)
        {
            if (name.StartsWith(".vs/", StringComparison.Ordinal))
                return;

            if (name.Equals(".gitmodules", StringComparison.Ordinal))
            {
                _updateSubmodules = DateTime.Now.AddSeconds(1).ToFileTime();
                _updateWC = DateTime.Now.AddSeconds(1).ToFileTime();
                return;
            }

            lock (_lockSubmodule)
            {
                foreach (var submodule in _submodules)
                {
                    if (name.StartsWith(submodule, StringComparison.Ordinal))
                    {
                        _updateSubmodules = DateTime.Now.AddSeconds(1).ToFileTime();
                        return;
                    }
                }
            }

            _updateWC = DateTime.Now.AddSeconds(1).ToFileTime();
        }

        private readonly IRepository _repo = null;
        private List<FileSystemWatcher> _watchers = [];
        private Timer _timer = null;
        private int _lockCount = 0;
        private long _updateWC = 0;
        private long _updateBranch = 0;
        private long _updateSubmodules = 0;
        private long _updateStashes = 0;
        private long _updateTags = 0;

        private readonly Lock _lockSubmodule = new();
        private List<string> _submodules = new List<string>();
    }
}
