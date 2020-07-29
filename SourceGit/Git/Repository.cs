using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using System.Xml.Serialization;

namespace SourceGit.Git {

    /// <summary>
    ///     Git repository
    /// </summary>
    public class Repository {

        #region HOOKS
        public static Action<Repository> OnOpen = null;
        public static Action OnClose = null;
        [XmlIgnore] public Action<string> OnNavigateCommit = null;
        [XmlIgnore] public Action OnWorkingCopyChanged = null;
        [XmlIgnore] public Action OnTagChanged = null;
        [XmlIgnore] public Action OnStashChanged = null;
        [XmlIgnore] public Action OnBranchChanged = null;
        [XmlIgnore] public Action OnCommitsChanged = null;
        [XmlIgnore] public Action OnSubmoduleChanged = null;
        #endregion

        #region PROPERTIES_SAVED
        /// <summary>
        ///     Storage path.
        /// </summary>
        public string Path { get; set; }
        /// <summary>
        ///     Display name.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        ///     Owner group.
        /// </summary>
        public string GroupId { get; set; }
        /// <summary>
        ///     Last open time(File time format).
        /// </summary>
        public long LastOpenTime { get; set; }
        /// <summary>
        ///     Filters for logs.
        /// </summary>
        public List<string> LogFilters { get; set; } = new List<string>();
        /// <summary>
        ///     Last 10 Commit message.
        /// </summary>
        public List<string> CommitMsgRecords { get; set; } = new List<string>();
        /// <summary>
        ///     Commit template.
        /// </summary>
        public string CommitTemplate { get; set; }
        #endregion

        #region PROPERTIES_RUNTIME
        [XmlIgnore] public Repository Parent = null;
        [XmlIgnore] public string GitDir = null;

        private List<Remote> cachedRemotes = new List<Remote>();
        private List<Branch> cachedBranches = new List<Branch>();
        private List<Tag> cachedTags = new List<Tag>();
        private FileSystemWatcher gitDirWatcher = null;
        private FileSystemWatcher workingCopyWatcher = null;
        private DispatcherTimer timer = null;
        private bool isWatcherDisabled = false;
        private long nextUpdateTags = 0;
        private long nextUpdateLocalChanges = 0;
        private long nextUpdateStashes = 0;
        private long nextUpdateTree = 0;

        private string featurePrefix = null;
        private string releasePrefix = null;
        private string hotfixPrefix = null;
        #endregion

        #region METHOD_PROCESS
        /// <summary>
        ///     Read git config
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetConfig(string key) {
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = Preference.Instance.GitExecutable;
            startInfo.Arguments = $"config {key}";
            startInfo.WorkingDirectory = Path;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;

            var proc = new Process() { StartInfo = startInfo };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            proc.Close();

            return output.Trim();
        }

        /// <summary>
        ///     Configure git.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SetConfig(string key, string value) {
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = Preference.Instance.GitExecutable;
            startInfo.Arguments = $"config {key} \"{value}\"";
            startInfo.WorkingDirectory = Path;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            var proc = new Process() { StartInfo = startInfo };
            proc.Start();
            proc.WaitForExit();
            proc.Close();
        }

        /// <summary>
        ///     Run git command without repository.
        /// </summary>
        /// <param name="cwd">Working directory.</param>
        /// <param name="args">Arguments for running git command.</param>
        /// <param name="outputHandler">Handler for output.</param>
        /// <param name="includeError">Handle error as output.</param>
        /// <returns>Errors if exists.</returns>
        public static string RunCommand(string cwd, string args, Action<string> outputHandler, bool includeError = false) {
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = Preference.Instance.GitExecutable;
            startInfo.Arguments = "--no-pager -c core.quotepath=off " + args;
            startInfo.WorkingDirectory = cwd;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardErrorEncoding = Encoding.UTF8;

            var progressFilter = new Regex(@"\d+\%");
            var errs = new List<string>();
            var proc = new Process() { StartInfo = startInfo };

            proc.OutputDataReceived += (o, e) => {
                if (e.Data == null) return;
                outputHandler?.Invoke(e.Data);
            };
            proc.ErrorDataReceived += (o, e) => {
                if (e.Data == null) return;
                if (includeError) outputHandler?.Invoke(e.Data);
                if (string.IsNullOrEmpty(e.Data)) return;
                if (progressFilter.IsMatch(e.Data)) return;
                if (e.Data.StartsWith("remote: Counting objects:", StringComparison.Ordinal)) return;
                errs.Add(e.Data);
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            int exitCode = proc.ExitCode;
            proc.Close();

            if (exitCode != 0 && errs.Count > 0) {
                return string.Join("\n", errs);
            } else {
                return null;
            }
        }

        /// <summary>
        ///     Create process for reading outputs/errors using git.exe
        /// </summary>
        /// <param name="args">Arguments for running git command.</param>
        /// <param name="outputHandler">Handler for output.</param>
        /// <param name="includeError">Handle error as output.</param>
        /// <returns>Errors if exists.</returns>
        public string RunCommand(string args, Action<string> outputHandler, bool includeError = false) {
            return RunCommand(Path, args, outputHandler, includeError);
        }

        /// <summary>
        ///     Create process and redirect output to file.
        /// </summary>
        /// <param name="args">Git command arguments.</param>
        /// <param name="redirectTo">File path to redirect output into.</param>
        public void RunAndRedirect(string args, string redirectTo) {
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = Preference.Instance.GitExecutable;
            startInfo.Arguments = "--no-pager " + args;
            startInfo.WorkingDirectory = Path;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            var proc = new Process() { StartInfo = startInfo };
            proc.Start();

            using (var writer = new FileStream(redirectTo, FileMode.OpenOrCreate)) {
                proc.StandardOutput.BaseStream.CopyTo(writer);
            }

            proc.WaitForExit();
            proc.Close();
        }

        /// <summary>
        ///     Assert command result and then update branches and commits.
        /// </summary>
        /// <param name="err"></param>
        public void AssertCommand(string err) {
            if (!string.IsNullOrEmpty(err)) App.RaiseError(err);

            Branches(true);
            OnBranchChanged?.Invoke();
            OnCommitsChanged?.Invoke();
            OnWorkingCopyChanged?.Invoke();
            OnTagChanged?.Invoke();

            isWatcherDisabled = false;
        }
        #endregion

        #region METHOD_VALIDATIONS
        /// <summary>
        ///     Is valid git directory.
        /// </summary>
        /// <param name="path">Local path.</param>
        /// <returns></returns>
        public static bool IsValid(string path) {
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = Preference.Instance.GitExecutable;
            startInfo.Arguments = "rev-parse --git-dir";
            startInfo.WorkingDirectory = path;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            try {
                var proc = new Process() { StartInfo = startInfo };
                proc.Start();
                proc.WaitForExit();

                var test = proc.ExitCode == 0;
                proc.Close();
                return test;
            } catch {
                return false;
            }            
        }

        /// <summary>
        ///     Is remote url valid.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IsValidUrl(string url) {
            return !string.IsNullOrEmpty(url)
                && (url.StartsWith("http://", StringComparison.Ordinal)
                || url.StartsWith("https://", StringComparison.Ordinal)
                || url.StartsWith("git://", StringComparison.Ordinal)
                || url.StartsWith("ssh://", StringComparison.Ordinal)
                || url.StartsWith("file://", StringComparison.Ordinal));
        }
        #endregion

        #region METHOD_OPEN_CLOSE
        /// <summary>
        ///     Open repository.
        /// </summary>
        public void Open() {
            LastOpenTime = DateTime.Now.ToFileTime();
            isWatcherDisabled = false;

            GitDir = ".git";
            RunCommand("rev-parse --git-dir", line => {
                GitDir = line;
            });
            if (!System.IO.Path.IsPathRooted(GitDir)) GitDir = System.IO.Path.Combine(Path, GitDir);
            
            var checkGitDir = new DirectoryInfo(GitDir);
            if (!checkGitDir.Exists) {
                App.RaiseError("GIT_DIR for this repository NOT FOUND!");
                return;
            } else {
                GitDir = checkGitDir.FullName;
            }

            gitDirWatcher = new FileSystemWatcher();
            gitDirWatcher.Path = GitDir;
            gitDirWatcher.Filter = "*";
            gitDirWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.FileName;
            gitDirWatcher.IncludeSubdirectories = true;
            gitDirWatcher.Created += OnGitDirFSChanged;
            gitDirWatcher.Renamed += OnGitDirFSChanged;
            gitDirWatcher.Changed += OnGitDirFSChanged;
            gitDirWatcher.Deleted += OnGitDirFSChanged;
            gitDirWatcher.EnableRaisingEvents = true;

            workingCopyWatcher = new FileSystemWatcher();
            workingCopyWatcher.Path = Path;
            workingCopyWatcher.Filter = "*";
            workingCopyWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.FileName;
            workingCopyWatcher.IncludeSubdirectories = true;
            workingCopyWatcher.Created += OnWorkingCopyFSChanged;
            workingCopyWatcher.Renamed += OnWorkingCopyFSChanged;
            workingCopyWatcher.Changed += OnWorkingCopyFSChanged;
            workingCopyWatcher.Deleted += OnWorkingCopyFSChanged;
            workingCopyWatcher.EnableRaisingEvents = true;

            timer = new DispatcherTimer();
            timer.Tick += Tick;
            timer.Interval = TimeSpan.FromSeconds(.1);
            timer.Start();

            featurePrefix = GetConfig("gitflow.prefix.feature");
            releasePrefix = GetConfig("gitflow.prefix.release");
            hotfixPrefix = GetConfig("gitflow.prefix.hotfix");

            OnOpen?.Invoke(this);
        }

        /// <summary>
        ///     Close repository.
        /// </summary>
        public void Close() {
            OnBranchChanged = null;
            OnCommitsChanged = null;
            OnTagChanged = null;
            OnStashChanged = null;
            OnWorkingCopyChanged = null;
            OnNavigateCommit = null;
            OnSubmoduleChanged = null;

            cachedBranches.Clear();
            cachedRemotes.Clear();
            cachedTags.Clear();

            gitDirWatcher.EnableRaisingEvents = false;
            workingCopyWatcher.EnableRaisingEvents = false;
            gitDirWatcher.Dispose();
            workingCopyWatcher.Dispose();
            timer.Stop();

            gitDirWatcher = null;
            workingCopyWatcher = null;
            timer = null;
            featurePrefix = null;
            releasePrefix = null;
            hotfixPrefix = null;

            OnClose?.Invoke();
        }
        #endregion

        #region METHOD_WATCHER
        public void SetWatcherEnabled(bool enabled) {
            isWatcherDisabled = !enabled;
        }

        private void Tick(object sender, EventArgs e) {
            if (isWatcherDisabled) {
                nextUpdateLocalChanges = 0;
                nextUpdateStashes = 0;
                nextUpdateTags = 0;
                nextUpdateTree = 0;
                return;
            }

            var now = DateTime.Now.ToFileTime();
            if (nextUpdateLocalChanges > 0 && now >= nextUpdateLocalChanges) {
                nextUpdateLocalChanges = 0;
                OnWorkingCopyChanged?.Invoke();
            }

            if (nextUpdateTags > 0 && now >= nextUpdateTags) {
                nextUpdateTags = 0;
                OnTagChanged?.Invoke();
            }

            if (nextUpdateStashes > 0 && now >= nextUpdateStashes) {
                nextUpdateStashes = 0;
                OnStashChanged?.Invoke();
            }

            if (nextUpdateTree > 0 && now >= nextUpdateTree) {
                nextUpdateTree = 0;
                Branches(true);
                OnBranchChanged?.Invoke();
                OnCommitsChanged?.Invoke();
            }
        }

        private void OnGitDirFSChanged(object sender, FileSystemEventArgs e) {
            if (string.IsNullOrEmpty(e.Name)) return;
            if (e.Name.StartsWith("index")) return;

            if (e.Name.StartsWith("refs\\tags", StringComparison.Ordinal)) {
                nextUpdateTags = DateTime.Now.AddSeconds(.5).ToFileTime();
            } else if (e.Name.StartsWith("refs\\stash", StringComparison.Ordinal)) {
                nextUpdateStashes = DateTime.Now.AddSeconds(.5).ToFileTime();
            } else if (e.Name.EndsWith("_HEAD", StringComparison.Ordinal) ||
                e.Name.StartsWith("refs\\heads", StringComparison.Ordinal) ||
                e.Name.StartsWith("refs\\remotes", StringComparison.Ordinal)) {
                nextUpdateTree = DateTime.Now.AddSeconds(.5).ToFileTime();
            }
        }

        private void OnWorkingCopyFSChanged(object sender, FileSystemEventArgs e) {
            if (string.IsNullOrEmpty(e.Name)) return;
            if (e.Name == ".git" || e.Name.StartsWith(".git\\")) return;

            nextUpdateLocalChanges = DateTime.Now.AddSeconds(1.5).ToFileTime();
        }
        #endregion

        #region METHOD_GITCOMMANDS
        /// <summary>
        ///     Clone repository.
        /// </summary>
        /// <param name="url">Remote repository URL</param>
        /// <param name="folder">Folder to clone into</param>
        /// <param name="name">Local name</param>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        public static Repository Clone(string url, string folder, string rName, string lName, Action<string> onProgress) {
            string RemoteName;
            if (rName != null) {
                RemoteName = $" --origin {rName}";
            } else {
                RemoteName = null;
            }

            var errs = RunCommand(folder, $"-c credential.helper=manager clone --progress --verbose {RemoteName} --recurse-submodules {url} {lName}", line => {
                if (line != null) onProgress?.Invoke(line);
            }, true);

            if (errs != null) {
                App.RaiseError(errs);
                return null;
            }

            var path = new DirectoryInfo(folder + "/" + lName).FullName;
            var repo = Preference.Instance.AddRepository(path, "");
            return repo;
        }

        /// <summary>
        ///     Fetch remote changes
        /// </summary>
        /// <param name="remote"></param>
        /// <param name="submod">submod</param>
        /// <param name="prune"></param>
        /// <param name="onProgress"></param>
        public void Fetch(Remote remote, string submod, bool prune, Action<string> onProgress) {
            isWatcherDisabled = true;

            var args = $"-c credential.helper=manager fetch --progress --verbose {submod} ";

            if (prune) args += "--prune ";

            if (remote == null) {
                args += "--all";
            } else {
                args += remote.Name;
            }

            var errs = RunCommand(args, line => {
                if (line != null) onProgress?.Invoke(line);
            }, true);

            OnSubmoduleChanged?.Invoke();

            AssertCommand(errs);
        }

        /// <summary>
        ///     Pull remote changes.
        /// </summary>
        /// <param name="remote">remote</param>
        /// <param name="branch">branch</param>
        /// <param name="submod">submod</param>
        /// <param name="onProgress">Progress message handler.</param>
        /// <param name="rebase">Use rebase instead of merge.</param>
        /// <param name="autostash">Auto stash local changes.</param>
        /// <param name="onProgress">Progress message handler.</param>
        public void Pull(string remote, string branch, string submod, Action<string> onProgress, bool rebase = false, bool autostash = false) {
            isWatcherDisabled = true;

            var args = $"-c credential.helper=manager pull --verbose --progress {submod} ";
            var needPopStash = false;

            if (rebase) args += "--rebase ";
            if (autostash) {
                if (rebase) {
                    args += "--autostash ";
                } else {
                    var changes = LocalChanges();
                    if (changes.Count > 0) {
                        var fatal = RunCommand("stash push -u -m \"PULL_AUTO_STASH\"", null);
                        if (fatal != null) {
                            App.RaiseError(fatal);
                            isWatcherDisabled = false;
                            return;
                        }
                        needPopStash = true;
                    }
                }
            }

            var errs = RunCommand(args + remote + " " + branch, line => {
                if (line != null) onProgress?.Invoke(line);
            }, true);

            OnSubmoduleChanged?.Invoke();

            AssertCommand(errs);

            if (needPopStash) RunCommand("stash pop -q stash@{0}", null);
        }

        /// <summary>
        ///     Push local branch to remote.
        /// </summary>
        /// <param name="remote">Remote</param>
        /// <param name="localBranch">Local branch name</param>
        /// <param name="remoteBranch">Remote branch name</param>
        /// <param name="submod">submod</param>
        /// <param name="onProgress">Progress message handler.</param>
        /// <param name="withTags">Push tags</param>
        /// <param name="track">Create track reference</param>
        /// <param name="force">Force push</param>
        public void Push(string remote, string localBranch, string remoteBranch,  string submod, Action<string> onProgress, bool withTags = false, bool track = false, bool force = false) {
            isWatcherDisabled = true;

            var args = $"-c credential.helper=manager push --progress --verbose {submod} ";

            if (withTags) args += "--tags ";
            if (track) args += "-u ";
            if (force) args += "--force-with-lease ";

            var errs = RunCommand(args + remote + " " + localBranch + ":" + remoteBranch, line => {
                if (line != null) onProgress?.Invoke(line);
            }, true);

            AssertCommand(errs);
        }

        /// <summary>
        ///     Apply patch.
        /// </summary>
        /// <param name="patch"></param>
        /// <param name="ignoreSpaceChanges"></param>
        /// <param name="whitespaceMode"></param>
        public void Apply(string patch, bool ignoreSpaceChanges, string whitespaceMode) {
            isWatcherDisabled = true;

            var args = "apply ";
            if (ignoreSpaceChanges) args += "--ignore-whitespace ";
            else args += $"--whitespace={whitespaceMode} ";

            var errs = RunCommand($"{args} \"{patch}\"", null);
            if (errs != null) {
                App.RaiseError(errs);
            } else {
                OnWorkingCopyChanged?.Invoke();
            }

            isWatcherDisabled = false;
        }

        /// <summary>
        ///     Revert given commit.
        /// </summary>
        /// <param name="commit"></param>
        /// <param name="autoCommit"></param>
        public void Revert(string commit, bool autoCommit) {
            isWatcherDisabled = true;

            var errs = RunCommand($"revert {commit} --no-edit" + (autoCommit ? "" : " --no-commit"), null);
            AssertCommand(errs);
        } 

        /// <summary>
        ///     Checkout
        /// </summary>
        /// <param name="option">Options.</param>
        public void Checkout(string option) {
            isWatcherDisabled = true;

            var errs = RunCommand($"checkout {option}", null);
            AssertCommand(errs);
        }

        /// <summary>
        ///     Merge given branch into current.
        /// </summary>
        /// <param name="branch"></param>
        /// <param name="option"></param>
        public void Merge(string branch, string option) {
            isWatcherDisabled = true;

            var errs = RunCommand($"merge {branch} {option}", null);
            AssertCommand(errs);
        }

        /// <summary>
        ///     Rebase current branch to revision
        /// </summary>
        /// <param name="revision"></param>
        /// <param name="autoStash"></param>
        public void Rebase(string revision, bool autoStash) {
            isWatcherDisabled = true;

            var args = $"rebase ";
            if (autoStash) args += "--autostash ";
            args += revision;

            var errs = RunCommand(args, null);
            AssertCommand(errs);
        }

        /// <summary>
        ///     Reset.
        /// </summary>
        /// <param name="revision"></param>
        /// <param name="mode"></param>
        public void Reset(string revision, string mode = "") {
            isWatcherDisabled = true;

            var errs = RunCommand($"reset {mode} {revision}", null);
            AssertCommand(errs);
        }

        /// <summary>
        ///     Cherry pick commit.
        /// </summary>
        /// <param name="commit"></param>
        /// <param name="noCommit"></param>
        public void CherryPick(string commit, bool noCommit) {
            isWatcherDisabled = true;

            var args = "cherry-pick ";
            args += noCommit ? "-n " : "--ff ";
            args += commit;

            var errs = RunCommand(args, null);
            AssertCommand(errs);
        }

        /// <summary>
        ///     Stage(add) files to index.
        /// </summary>
        /// <param name="files"></param>
        public void Stage(params string[] files) {
            isWatcherDisabled = true;

            var args = "add";
            if (files == null || files.Length == 0) {
                args += " .";
            } else {
                args += " --";
                foreach (var file in files) args += $" \"{file}\"";
            }

            var errs = RunCommand(args, null);
            if (errs != null) App.RaiseError(errs);

            OnWorkingCopyChanged?.Invoke();
            isWatcherDisabled = false;
        }

        /// <summary>
        ///     Unstage files from index
        /// </summary>
        /// <param name="files"></param>
        public void Unstage(params string[] files) {
            isWatcherDisabled = true;

            var args = "reset";
            if (files != null && files.Length > 0) {
                args += " --";
                foreach (var file in files) args += $" \"{file}\"";
            }

            var errs = RunCommand(args, null);
            if (errs != null) App.RaiseError(errs);

            OnWorkingCopyChanged?.Invoke();
            isWatcherDisabled = false;
        }

        /// <summary>
        ///     Discard changes.
        /// </summary>
        /// <param name="changes"></param>
        public void Discard(List<Change> changes) {
            isWatcherDisabled = true;

            if (changes == null || changes.Count == 0) {
                var errs = RunCommand("reset --hard HEAD", null);
                if (errs != null) {
                    App.RaiseError(errs);
                    isWatcherDisabled = false;
                    return;
                }

                RunCommand("clean -qfd", null);
            } else {
                foreach (var change in changes) {
                    if (change.WorkTree == Change.Status.Untracked || change.WorkTree == Change.Status.Added) {
                        RunCommand($"clean -qfd -- \"{change.Path}\"", null);
                    } else {
                        RunCommand($"checkout -f -- \"{change.Path}\"", null);
                    }
                }
            }
            
            OnWorkingCopyChanged?.Invoke();
            isWatcherDisabled = false;
        }

        /// <summary>
        ///     Commit
        /// </summary>
        /// <param name="message"></param>
        /// <param name="amend"></param>
        public bool DoCommit(string message, bool amend) {
            isWatcherDisabled = true;

            var file = System.IO.Path.GetTempFileName();
            File.WriteAllText(file, message);

            var args = $"commit --file=\"{file}\"";
            if (amend) args += " --amend --no-edit";
            var errs = RunCommand(args, null);
            AssertCommand(errs);

            var branch = CurrentBranch();
            OnNavigateCommit?.Invoke(branch.Head);
            return string.IsNullOrEmpty(errs);
        }

        /// <summary>
        ///     Get all remotes of this repository.
        /// </summary>
        /// <param name="bForceReload">Force reload</param>
        /// <returns>Remote collection</returns>
        public List<Remote> Remotes(bool bForceReload = false) {
            if (cachedRemotes.Count == 0 || bForceReload) {
                cachedRemotes = Remote.Load(this);
            }

            return cachedRemotes;
        }

        /// <summary>
        ///     Local changes in working copy.
        /// </summary>
        /// <returns>Changes.</returns>
        public List<Change> LocalChanges() {
            List<Change> changes = new List<Change>();
            RunCommand("status -uall --ignore-submodules=dirty --porcelain", line => {
                if (!string.IsNullOrEmpty(line)) {
                    var change = Change.Parse(line);
                    if (change != null) changes.Add(change);
                }
            });
            return changes;
        }

        /// <summary>
        ///     Get total commit count.
        /// </summary>
        /// <returns>Number of total commits.</returns>
        public int TotalCommits() {
            int count = 0;
            RunCommand("rev-list --all --count", line => {
                if (!string.IsNullOrEmpty(line)) count = int.Parse(line.Trim());
            });
            return count;
        }

        /// <summary>
        ///     Load commits.
        /// </summary>
        /// <param name="limit">Extra limit arguments for `git log`</param>
        /// <returns>Commit collection</returns>
        public List<Commit> Commits(string limit = null) {
            return Commit.Load(this, (limit == null ? "" : limit)); ;
        }

        /// <summary>
        ///     Load all branches.
        /// </summary>
        /// <param name="bForceReload">Force reload.</param>
        /// <returns>Branches collection.</returns>
        public List<Branch> Branches(bool bForceReload = false) {
            if (cachedBranches.Count == 0 || bForceReload) {
                cachedBranches = Branch.Load(this);
            }

            if (IsGitFlowEnabled()) {
                foreach (var b in cachedBranches) {
                    if (b.IsLocal) {
                        if (b.Name.StartsWith(featurePrefix)) {
                            b.Kind = Branch.Type.Feature;
                        } else if (b.Name.StartsWith(releasePrefix)) {
                            b.Kind = Branch.Type.Release;
                        } else if (b.Name.StartsWith(hotfixPrefix)) {
                            b.Kind = Branch.Type.Hotfix;
                        }
                    }
                }
            }            

            return cachedBranches;
        }

        /// <summary>
        ///     Get current branch
        /// </summary>
        /// <returns></returns>
        public Branch CurrentBranch() {
            foreach (var b in cachedBranches) {
                if (b.IsCurrent) return b;
            }

            return null;
        }

        /// <summary>
        ///     Load all tags.
        /// </summary>
        /// <param name="bForceReload"></param>
        /// <returns></returns>
        public List<Tag> Tags(bool bForceReload = false) {
            if (cachedTags.Count == 0 || bForceReload) {
                cachedTags = Tag.Load(this);
            }

            return cachedTags;
        }

        /// <summary>
        ///     Get all stashes
        /// </summary>
        /// <returns></returns>
        public List<Stash> Stashes() {
            var reflog = new Regex(@"^Reflog: refs/(stash@\{\d+\}).*$");
            var stashes = new List<Stash>();
            var current = null as Stash;

            var errs = RunCommand("stash list --pretty=raw", line => {
                if (line.StartsWith("commit ")) {
                    if (current != null && !string.IsNullOrEmpty(current.Name)) stashes.Add(current);
                    current = new Stash() { SHA = line.Substring(7, 8) };
                    return;
                }

                if (current == null) return;

                if (line.StartsWith("Reflog: refs/stash@")) {
                    var match = reflog.Match(line);
                    if (match.Success) current.Name = match.Groups[1].Value;
                } else if (line.StartsWith("Reflog message: ")) {
                    current.Message = line.Substring(16);
                } else if (line.StartsWith("author ")) {
                    current.Author.Parse(line);
                }
            });

            if (current != null) stashes.Add(current);
            if (errs != null) App.RaiseError(errs);
            return stashes;
        }

        /// <summary>
        ///     Get all submodules
        /// </summary>
        /// <returns></returns>
        public List<string> Submodules() {
            var test = new Regex(@"^[\-\+ ][0-9a-f]+\s(.*)\(.*\)$");
            var modules = new List<string>();

            var errs = RunCommand("submodule status", line => {
                var match = test.Match(line);
                if (!match.Success) return;

                modules.Add(match.Groups[1].Value);
            });

            return modules;
        }

        /// <summary>
        ///     Add submodule
        /// </summary>
        /// <param name="url"></param>
        /// <param name="localPath"></param>
        /// <param name="recursive"></param>
        /// <param name="onProgress"></param>
        public void AddSubmodule(string url, string localPath, bool recursive, Action<string> onProgress) {
            isWatcherDisabled = true;

            var errs = RunCommand($"submodule add {url} {localPath}", onProgress, true);
            if (errs == null) {
                if (recursive) RunCommand($"submodule update --init --recursive -- {localPath}", onProgress, true);
                OnWorkingCopyChanged?.Invoke();
                OnSubmoduleChanged?.Invoke();
            } else {
                App.RaiseError(errs);
            }

            isWatcherDisabled = false;
        }

        /// <summary>
        ///     Update submodule.
        /// </summary>
        public void UpdateSubmodule() {
            isWatcherDisabled = true;

            var errs = RunCommand("submodule update --rebase --remote", null);
            if (errs != null) {
                App.RaiseError(errs);
            } else {
                OnSubmoduleChanged?.Invoke();
            }

            isWatcherDisabled = false;
        }

        /// <summary>
        ///     Blame file.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="revision"></param>
        /// <returns></returns>
        public Blame BlameFile(string file, string revision) {
            var regex = new Regex(@"^\^?([0-9a-f]+)\s+.*\((.*)\s+(\d+)\s+[\-\+]?\d+\s+\d+\) (.*)");
            var blame = new Blame();
            var current = null as Blame.Block;

            var errs = RunCommand($"blame -t {revision} -- \"{file}\"", line => {
                if (blame.IsBinary) return;
                if (string.IsNullOrEmpty(line)) return;

                if (line.IndexOf('\0') >= 0) {
                    blame.IsBinary = true;
                    blame.Blocks.Clear();
                    return;
                }

                var match = regex.Match(line);
                if (!match.Success) return;

                var commit = match.Groups[1].Value;
                var data = match.Groups[4].Value;
                if (current != null && current.CommitSHA == commit) {
                    current.Content = current.Content + "\n" + data;
                } else {
                    var timestamp = int.Parse(match.Groups[3].Value);
                    var when = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

                    current = new Blame.Block() {
                        CommitSHA = commit,
                        Author = match.Groups[2].Value,
                        Time = when,
                        Content = data,
                    };

                    if (current.Author == null) current.Author = "";
                    blame.Blocks.Add(current);
                }

                blame.LineCount++;
            });

            if (errs != null) App.RaiseError(errs);
            return blame;
        }

        /// <summary>
        ///     Get file size.
        /// </summary>
        /// <param name="sha"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public long GetFileSize(string sha, string path) {
            long size = 0;
            RunCommand($"cat-file -s {sha}:\"{path}\"", line => {
                if (!long.TryParse(line, out size)) size = 0;
            });
            return size;
        }
        #endregion

        #region METHOD_GITFLOW
        /// <summary>
        ///     Check if git-flow feature enabled
        /// </summary>
        /// <returns></returns>
        public bool IsGitFlowEnabled() {
            return !string.IsNullOrEmpty(featurePrefix)
                && !string.IsNullOrEmpty(releasePrefix)
                && !string.IsNullOrEmpty(hotfixPrefix);
        }

        /// <summary>
        ///     Get git-flow branch prefix.
        /// </summary>
        /// <returns></returns>
        public string GetFeaturePrefix() { return featurePrefix; }
        public string GetReleasePrefix() { return releasePrefix; }
        public string GetHotfixPrefix() { return hotfixPrefix; }

        /// <summary>
        ///     Enable git-flow
        /// </summary>
        /// <param name="master"></param>
        /// <param name="develop"></param>
        /// <param name="feature"></param>
        /// <param name="release"></param>
        /// <param name="hotfix"></param>
        /// <param name="version"></param>
        public void EnableGitFlow(string master, string develop, string feature, string release, string hotfix, string version = "") {
            isWatcherDisabled = true;
            
            var branches = Branches();
            var masterBranch = branches.Find(b => b.Name == master);
            var devBranch = branches.Find(b => b.Name == develop);
            var refreshBranches = false;

            if (masterBranch == null) {
                var errs = RunCommand($"branch --no-track {master}", null);
                if (errs != null) {
                    App.RaiseError(errs);
                    isWatcherDisabled = false;
                    return;
                }

                refreshBranches = true;
            }

            if (devBranch == null) {
                var errs = RunCommand($"branch --no-track {develop}", null);
                if (errs != null) {
                    App.RaiseError(errs);
                    if (refreshBranches) {
                        Branches(true);
                        OnBranchChanged?.Invoke();
                        OnCommitsChanged?.Invoke();
                        OnWorkingCopyChanged?.Invoke();
                    }
                    isWatcherDisabled = false;
                    return;
                }

                refreshBranches = true;
            }
            
            SetConfig("gitflow.branch.master", master);
            SetConfig("gitflow.branch.develop", develop);
            SetConfig("gitflow.prefix.feature", feature);
            SetConfig("gitflow.prefix.bugfix", "bugfix");
            SetConfig("gitflow.prefix.release", release);
            SetConfig("gitflow.prefix.hotfix", hotfix);
            SetConfig("gitflow.prefix.support", "support");
            SetConfig("gitflow.prefix.versiontag", version);
            
            RunCommand("flow init -d", null);

            featurePrefix = GetConfig("gitflow.prefix.feature");
            releasePrefix = GetConfig("gitflow.prefix.release");
            hotfixPrefix = GetConfig("gitflow.prefix.hotfix");

            if (!IsGitFlowEnabled()) App.RaiseError("Initialize Git-flow failed!");

            if (refreshBranches) {
                Branches(true);
                OnBranchChanged?.Invoke();
                OnCommitsChanged?.Invoke();
                OnWorkingCopyChanged?.Invoke();
            }

            isWatcherDisabled = false;
        }

        /// <summary>
        ///     Start git-flow branch
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        public void StartGitFlowBranch(Branch.Type type, string name) {
            isWatcherDisabled = true;

            string args;
            switch (type) {
            case Branch.Type.Feature: args = $"flow feature start {name}"; break;
            case Branch.Type.Release: args = $"flow release start {name}"; break;
            case Branch.Type.Hotfix: args = $"flow hotfix start {name}"; break;
            default:
                App.RaiseError("Bad git-flow branch type!");
                return;
            }

            var errs = RunCommand(args, null);
            AssertCommand(errs);
        }

        /// <summary>
        ///     Finish git-flow branch
        /// </summary>
        /// <param name="branch"></param>
        public void FinishGitFlowBranch(Branch branch) {
            isWatcherDisabled = true;

            string args;
            switch (branch.Kind) {
            case Branch.Type.Feature:
                args = $"flow feature finish {branch.Name.Substring(featurePrefix.Length)}"; 
                break;
            case Branch.Type.Release:
                var releaseName = branch.Name.Substring(releasePrefix.Length);
                args = $"flow release finish {releaseName} -m \"Release done\""; 
                break;
            case Branch.Type.Hotfix:
                var hotfixName = branch.Name.Substring(hotfixPrefix.Length);
                args = $"flow hotfix finish {hotfixName} -m \"Hotfix done\""; 
                break;
            default:
                App.RaiseError("Bad git-flow branch type!");
                return;
            }

            var errs = RunCommand(args, null);
            AssertCommand(errs);
            OnTagChanged?.Invoke();
        }
        #endregion

        #region METHOD_COMMITMSG
        public void RecordCommitMessage(string message) {
            if (string.IsNullOrEmpty(message)) return;

            int exists = CommitMsgRecords.Count;
            if (exists > 0) {
                var last = CommitMsgRecords[0];
                if (last == message) return;
            }

            if (exists >= 10) {
                CommitMsgRecords.RemoveRange(9, exists - 9);
            }

            CommitMsgRecords.Insert(0, message);
        }
        #endregion
    }
}
