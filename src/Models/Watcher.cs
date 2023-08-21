using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SourceGit.Models {

    /// <summary>
    ///     文件系统更新监视
    /// </summary>
    public class Watcher {
        /// <summary>
        ///     打开仓库事件
        /// </summary>
        public static event Action<Repository> Opened;

        /// <summary>
        ///     仓库的书签变化了
        /// </summary>
        public static event Action<string, int> BookmarkChanged;

        /// <summary>
        ///     跳转到指定提交的事件
        /// </summary>
        public event Action<string> Navigate;
        /// <summary>
        ///     工作副本变更
        /// </summary>
        public event Action WorkingCopyChanged;
        /// <summary>
        ///     分支数据变更
        /// </summary>
        public event Action BranchChanged;
        /// <summary>
        ///     标签变更
        /// </summary>
        public event Action TagChanged;
        /// <summary>
        ///     贮藏变更
        /// </summary>
        public event Action StashChanged;
        /// <summary>
        ///     子模块变更
        /// </summary>
        public event Action SubmoduleChanged;
        /// <summary>
        ///     树更新
        /// </summary>
        public event Action SubTreeChanged;

        /// <summary>
        ///     打开仓库事件
        /// </summary>
        /// <param name="repo"></param>
        public static void Open(Repository repo) {
            if (all.ContainsKey(repo.Path)) {
                Opened?.Invoke(repo);
                return;
            }

            var watcher = new Watcher();
            watcher.Start(repo.Path, repo.GitDir);
            all.Add(repo.Path, watcher);
            repo.LastOpenTime = DateTime.Now.ToFileTime();

            Opened?.Invoke(repo);
        }

        /// <summary>
        ///     停止指定的监视器
        /// </summary>
        /// <param name="repoPath"></param>
        public static void Close(string repoPath) {
            if (!all.ContainsKey(repoPath)) return;
            all[repoPath].Stop();
            all.Remove(repoPath);
        }

        /// <summary>
        ///     取得一个仓库的监视器
        /// </summary>
        /// <param name="repoPath"></param>
        /// <returns></returns>
        public static Watcher Get(string repoPath) {
            if (all.ContainsKey(repoPath)) return all[repoPath];
            return null;
        }

        /// <summary>
        ///     暂停或启用监听
        /// </summary>
        /// <param name="repoPath"></param>
        /// <param name="enabled"></param>
        public static void SetEnabled(string repoPath, bool enabled) {
            if (all.ContainsKey(repoPath)) {
                var watcher = all[repoPath];
                if (enabled) {
                    if (watcher.lockCount > 0) watcher.lockCount--;
                } else {
                    watcher.lockCount++;
                }
            }
        }

        /// <summary>
        ///     设置仓库标签变化
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="bookmark"></param>
        public static void SetBookmark(string repo, int bookmark) {
            BookmarkChanged?.Invoke(repo, bookmark);
        }

        /// <summary>
        ///     跳转到指定的提交
        /// </summary>
        /// <param name="commit"></param>
        public void NavigateTo(string commit) {
            Navigate?.Invoke(commit);
        }

        /// <summary>
        ///     仅强制更新本地变化
        /// </summary>
        public void RefreshWC() {
            updateWC = 0;
            WorkingCopyChanged?.Invoke();
        }

        /// <summary>
        ///     通知更新子树列表
        /// </summary>
        public void RefreshSubTrees() {
            SubTreeChanged?.Invoke();
        }

        private void Start(string repo, string gitDir) {
            wcWatcher = new FileSystemWatcher();
            wcWatcher.Path = repo;
            wcWatcher.Filter = "*";
            wcWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime;
            wcWatcher.IncludeSubdirectories = true;
            wcWatcher.Created += OnWorkingCopyChanged;
            wcWatcher.Renamed += OnWorkingCopyChanged;
            wcWatcher.Changed += OnWorkingCopyChanged;
            wcWatcher.Deleted += OnWorkingCopyChanged;
            wcWatcher.EnableRaisingEvents = true;

            repoWatcher = new FileSystemWatcher();
            repoWatcher.Path = gitDir;
            repoWatcher.Filter = "*";
            repoWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.FileName;
            repoWatcher.IncludeSubdirectories = true;
            repoWatcher.Created += OnRepositoryChanged;
            repoWatcher.Renamed += OnRepositoryChanged;
            repoWatcher.Changed += OnRepositoryChanged;
            repoWatcher.Deleted += OnRepositoryChanged;
            repoWatcher.EnableRaisingEvents = true;

            timer = new Timer(Tick, null, 100, 100);
        }

        private void Stop() {
            repoWatcher.EnableRaisingEvents = false;
            repoWatcher.Dispose();
            repoWatcher = null;

            wcWatcher.EnableRaisingEvents = false;
            wcWatcher.Dispose();
            wcWatcher = null;

            timer.Dispose();
            timer = null;

            Navigate = null;
            WorkingCopyChanged = null;
            BranchChanged = null;
            TagChanged = null;
            StashChanged = null;
            SubmoduleChanged = null;
            SubTreeChanged = null;
        }

        private void OnRepositoryChanged(object o, FileSystemEventArgs e) {
            if (string.IsNullOrEmpty(e.Name)) return;

            if (e.Name.StartsWith("modules", StringComparison.Ordinal)) {
                updateSubmodules = DateTime.Now.AddSeconds(1).ToFileTime();
            } else if (e.Name.StartsWith("refs\\tags", StringComparison.Ordinal)) {
                updateTags = DateTime.Now.AddSeconds(.5).ToFileTime();
            } else if (e.Name.StartsWith("refs\\stash", StringComparison.Ordinal)) {
                updateStashes = DateTime.Now.AddSeconds(.5).ToFileTime();
            } else if (e.Name.Equals("HEAD", StringComparison.Ordinal) ||
                e.Name.StartsWith("refs\\heads\\", StringComparison.Ordinal) ||
                e.Name.StartsWith("refs\\remotes\\", StringComparison.Ordinal) ||
                e.Name.StartsWith("worktrees\\")) {
                updateBranch = DateTime.Now.AddSeconds(.5).ToFileTime();
            } else if (e.Name.StartsWith("objects\\", StringComparison.Ordinal) || e.Name.Equals("index", StringComparison.Ordinal)) {
                updateWC = DateTime.Now.AddSeconds(.5).ToFileTime();
            }
        }

        private void OnWorkingCopyChanged(object o, FileSystemEventArgs e) {
            if (string.IsNullOrEmpty(e.Name)) return;
            if (e.Name == ".git" || e.Name.StartsWith(".git\\", StringComparison.Ordinal)) return;

            updateWC = DateTime.Now.AddSeconds(1).ToFileTime();
        }

        private void Tick(object sender) {
            if (lockCount > 0) return;

            var now = DateTime.Now.ToFileTime();
            if (updateBranch > 0 && now > updateBranch) {
                BranchChanged?.Invoke();
                WorkingCopyChanged?.Invoke();
                updateBranch = 0;
                updateWC = 0;
            }

            if (updateWC > 0 && now > updateWC) {
                WorkingCopyChanged?.Invoke();
                updateWC = 0;
            }

            if (updateSubmodules > 0 && now > updateSubmodules) {
                SubmoduleChanged?.Invoke();
                updateSubmodules = 0;
            }

            if (updateStashes > 0 && now > updateStashes) {
                StashChanged?.Invoke();
                updateStashes = 0;
            }

            if (updateTags > 0 && now > updateTags) {
                TagChanged?.Invoke();
                updateTags = 0;
            }
        }

        #region PRIVATES
        private static Dictionary<string, Watcher> all = new Dictionary<string, Watcher>();

        private FileSystemWatcher repoWatcher = null;
        private FileSystemWatcher wcWatcher = null;
        private Timer timer = null;
        private int lockCount = 0;
        private long updateWC = 0;
        private long updateBranch = 0;
        private long updateSubmodules = 0;
        private long updateStashes = 0;
        private long updateTags = 0;
        #endregion
    }
}
