using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Models {
    public interface IRepository {
        string FullPath { get; set; }
        string GitDir { get; set; }

        void RefreshBranches();
        void RefreshTags();
        void RefreshCommits();
        void RefreshSubmodules();
        void RefreshWorkingCopyChanges();
        void RefreshStashes();
    }

    public class Watcher : IDisposable {
        public Watcher(IRepository repo) {
            _repo = repo;

            _wcWatcher = new FileSystemWatcher();
            _wcWatcher.Path = _repo.FullPath;
            _wcWatcher.Filter = "*";
            _wcWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime;
            _wcWatcher.IncludeSubdirectories = true;
            _wcWatcher.Created += OnWorkingCopyChanged;
            _wcWatcher.Renamed += OnWorkingCopyChanged;
            _wcWatcher.Changed += OnWorkingCopyChanged;
            _wcWatcher.Deleted += OnWorkingCopyChanged;
            _wcWatcher.EnableRaisingEvents = true;

            _repoWatcher = new FileSystemWatcher();
            _repoWatcher.Path = _repo.GitDir;
            _repoWatcher.Filter = "*";
            _repoWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.FileName;
            _repoWatcher.IncludeSubdirectories = true;
            _repoWatcher.Created += OnRepositoryChanged;
            _repoWatcher.Renamed += OnRepositoryChanged;
            _repoWatcher.Changed += OnRepositoryChanged;
            _repoWatcher.Deleted += OnRepositoryChanged;
            _repoWatcher.EnableRaisingEvents = true;

            _timer = new Timer(Tick, null, 100, 100);
        }

        public void SetEnabled(bool enabled) {
            if (enabled) {
                if (_lockCount > 0) _lockCount--;
            } else {
                _lockCount++;
            }
        }

        public void MarkBranchDirtyManually() {
            _updateBranch = DateTime.Now.ToFileTime() - 1;
        }

        public void MarkWorkingCopyDirtyManually() {
            _updateWC = DateTime.Now.ToFileTime() - 1;
        }

        public void Dispose() {
            _repoWatcher.EnableRaisingEvents = false;
            _repoWatcher.Created -= OnRepositoryChanged;
            _repoWatcher.Renamed -= OnRepositoryChanged;
            _repoWatcher.Changed -= OnRepositoryChanged;
            _repoWatcher.Deleted -= OnRepositoryChanged;
            _repoWatcher.Dispose();
            _repoWatcher = null;

            _wcWatcher.EnableRaisingEvents = false;
            _wcWatcher.Created -= OnWorkingCopyChanged;
            _wcWatcher.Renamed -= OnWorkingCopyChanged;
            _wcWatcher.Changed -= OnWorkingCopyChanged;
            _wcWatcher.Deleted -= OnWorkingCopyChanged;
            _wcWatcher.Dispose();
            _wcWatcher = null;

            _timer.Dispose();
            _timer = null;
        }

        private void Tick(object sender) {
            if (_lockCount > 0) return;

            var now = DateTime.Now.ToFileTime();
            if (_updateBranch > 0 && now > _updateBranch) {
                _updateBranch = 0;
                _updateWC = 0;

                if (_updateTags > 0) {
                    _updateTags = 0;
                    Task.Run(() => {
                        _repo.RefreshTags();
                        _repo.RefreshBranches();
                        _repo.RefreshCommits();
                    });
                } else {
                    Task.Run(() => {
                        _repo.RefreshBranches();
                        _repo.RefreshCommits();
                    });
                }

                Task.Run(_repo.RefreshWorkingCopyChanges);
            }

            if (_updateWC > 0 && now > _updateWC) {
                _updateWC = 0;
                Task.Run(_repo.RefreshWorkingCopyChanges);
            }

            if (_updateSubmodules > 0 && now > _updateSubmodules) {
                _updateSubmodules = 0;
                _repo.RefreshSubmodules();
            }

            if (_updateStashes > 0 && now > _updateStashes) {
                _updateStashes = 0;
                _repo.RefreshStashes();
            }

            if (_updateTags > 0 && now > _updateTags) {
                _updateTags = 0;
                _repo.RefreshTags();
                _repo.RefreshCommits();
            }
        }

        private void OnRepositoryChanged(object o, FileSystemEventArgs e) {
            if (string.IsNullOrEmpty(e.Name)) return;

            var name = e.Name.Replace("\\", "/");
            if (name.StartsWith("modules", StringComparison.Ordinal)) {
                _updateSubmodules = DateTime.Now.AddSeconds(1).ToFileTime();
            } else if (name.StartsWith("refs/tags", StringComparison.Ordinal)) {
                _updateTags = DateTime.Now.AddSeconds(.5).ToFileTime();
            } else if (name.StartsWith("refs/stash", StringComparison.Ordinal)) {
                _updateStashes = DateTime.Now.AddSeconds(.5).ToFileTime();
            } else if (name.Equals("HEAD", StringComparison.Ordinal) ||
                name.StartsWith("refs/heads/", StringComparison.Ordinal) ||
                name.StartsWith("refs/remotes/", StringComparison.Ordinal) ||
                name.StartsWith("worktrees/")) {
                _updateBranch = DateTime.Now.AddSeconds(.5).ToFileTime();
            } else if (name.StartsWith("objects/", StringComparison.Ordinal) || name.Equals("index", StringComparison.Ordinal)) {
                _updateWC = DateTime.Now.AddSeconds(1).ToFileTime();
            }
        }

        private void OnWorkingCopyChanged(object o, FileSystemEventArgs e) {
            if (string.IsNullOrEmpty(e.Name)) return;

            var name = e.Name.Replace("\\", "/");
            if (name == ".git" || name.StartsWith(".git/", StringComparison.Ordinal)) return;
            _updateWC = DateTime.Now.AddSeconds(1).ToFileTime();
        }

        private IRepository _repo = null;
        private FileSystemWatcher _repoWatcher = null;
        private FileSystemWatcher _wcWatcher = null;
        private Timer _timer = null;
        private int _lockCount = 0;
        private long _updateWC = 0;
        private long _updateBranch = 0;
        private long _updateSubmodules = 0;
        private long _updateStashes = 0;
        private long _updateTags = 0;
    }
}
