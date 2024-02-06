using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class Repository : ObservableObject, Models.IRepository {
        public string FullPath {
            get => _fullpath;
            set => SetProperty(ref _fullpath, value);
        }

        public string GitDir {
            get => _gitDir;
            set => SetProperty(ref _gitDir, value);
        }

        public AvaloniaList<string> Filters {
            get;
            private set;
        } = new AvaloniaList<string>();

        public AvaloniaList<string> CommitMessages {
            get;
            private set;
        } = new AvaloniaList<string>();

        [JsonIgnore]
        public bool IsVSCodeFound {
            get => !string.IsNullOrEmpty(Native.OS.VSCodeExecutableFile);
        }

        [JsonIgnore]
        public Models.GitFlow GitFlow {
            get => _gitflow;
            set => SetProperty(ref _gitflow, value);
        }

        [JsonIgnore]
        public int SelectedViewIndex {
            get => _selectedViewIndex;
            set {
                if (SetProperty(ref _selectedViewIndex, value)) {
                    switch (value) {
                    case 1:
                        SelectedView = _workingCopy;
                        break;
                    case 2:
                        SelectedView = _stashesPage;
                        break;
                    default:
                        SelectedView = _histories;
                        break;
                    }
                }
            }
        }

        [JsonIgnore]
        public object SelectedView {
            get => _selectedView;
            set => SetProperty(ref _selectedView, value);
        }

        [JsonIgnore]
        public List<Models.Remote> Remotes {
            get => _remotes;
            private set => SetProperty(ref _remotes, value);
        }

        [JsonIgnore]
        public List<Models.Branch> Branches {
            get => _branches;
            private set => SetProperty(ref _branches, value);
        }

        [JsonIgnore]
        public List<Models.BranchTreeNode> LocalBranchTrees {
            get => _localBranchTrees;
            private set => SetProperty(ref _localBranchTrees, value);
        }

        [JsonIgnore]
        public List<Models.BranchTreeNode> RemoteBranchTrees {
            get => _remoteBranchTrees;
            private set => SetProperty(ref _remoteBranchTrees, value);
        }

        [JsonIgnore]
        public List<Models.Tag> Tags {
            get => _tags;
            private set => SetProperty(ref _tags, value);
        }

        [JsonIgnore]
        public List<string> Submodules {
            get => _submodules;
            private set => SetProperty(ref _submodules, value);
        }

        [JsonIgnore]
        public int WorkingCopyChangesCount {
            get => _workingCopy.Count;
        }

        [JsonIgnore]
        public int StashesCount {
            get => _stashesPage.Count;
        }

        [JsonIgnore]
        public bool IsConflictBarVisible {
            get => _isConflictBarVisible;
            private set => SetProperty(ref _isConflictBarVisible, value);
        }

        [JsonIgnore]
        public bool HasUnsolvedConflict {
            get => _hasUnsolvedConflict;
            private set => SetProperty(ref _hasUnsolvedConflict, value);
        }

        [JsonIgnore]
        public bool CanCommitWithPush {
            get => _canCommitWithPush;
            private set => SetProperty(ref _canCommitWithPush, value);
        }

        [JsonIgnore]
        public bool IncludeUntracked {
            get => _includeUntracked;
            set {
                if (SetProperty(ref _includeUntracked, value)) {
                    Task.Run(RefreshWorkingCopyChanges);
                }
            }
        }

        [JsonIgnore]
        public bool IsSearching {
            get => _isSearching;
            set {
                if (SetProperty(ref _isSearching, value)) {
                    SearchedCommits = new List<Models.Commit>();
                    SearchCommitFilter = string.Empty;
                    if (value) SelectedViewIndex = 0;
                }
            }
        }

        [JsonIgnore]
        public string SearchCommitFilter {
            get => _searchCommitFilter;
            set => SetProperty(ref _searchCommitFilter, value);
        }

        [JsonIgnore]
        public List<Models.Commit> SearchedCommits {
            get => _searchedCommits;
            set => SetProperty(ref _searchedCommits, value);
        }

        public void Open() {
            _watcher = new Models.Watcher(this);
            _histories = new Histories(this);
            _workingCopy = new WorkingCopy(this);
            _stashesPage = new StashesPage(this);
            _selectedView = _histories;
            _selectedViewIndex = 0;
            _isConflictBarVisible = false;
            _hasUnsolvedConflict = false;

            Task.Run(() => {
                RefreshBranches();
                RefreshTags();
                RefreshCommits();
            });

            Task.Run(RefreshSubmodules);
            Task.Run(RefreshWorkingCopyChanges);
            Task.Run(RefreshStashes);
            Task.Run(RefreshGitFlow);
        }

        public void Close() {
            _watcher.Dispose();
            _watcher = null;
            _histories = null;
            _workingCopy = null;
            _stashesPage = null;
            _selectedView = null;
            _isSearching = false;
            _searchCommitFilter = string.Empty;

            _remotes.Clear();
            _branches.Clear();
            _localBranchTrees.Clear();
            _remoteBranchTrees.Clear();
            _tags.Clear();
            _submodules.Clear();
            _searchedCommits.Clear();

            GC.Collect();
        }

        public void OpenInFileManager() {
            Native.OS.OpenInFileManager(_fullpath);
        }

        public void OpenInVSCode() {
            Native.OS.OpenInVSCode(_fullpath);
        }

        public void OpenInTerminal() {
            Native.OS.OpenTerminal(_fullpath);
        }

        public void Fetch() {
            if (!PopupHost.CanCreatePopup()) return;

            if (Remotes.Count == 0) {
                App.RaiseException(_fullpath, "No remotes added to this repository!!!");
                return;
            }

            PopupHost.ShowPopup(new Fetch(this));
        }

        public void Pull() {
            if (!PopupHost.CanCreatePopup()) return;

            if (Remotes.Count == 0) {
                App.RaiseException(_fullpath, "No remotes added to this repository!!!");
                return;
            }

            PopupHost.ShowPopup(new Pull(this, null));
        }

        public void Push() {
            if (!PopupHost.CanCreatePopup()) return;

            if (Remotes.Count == 0) {
                App.RaiseException(_fullpath, "No remotes added to this repository!!!");
                return;
            }

            if (Branches.Find(x => x.IsCurrent) == null) App.RaiseException(_fullpath, "Can NOT found current branch!!!");
            PopupHost.ShowPopup(new Push(this, null));
        }

        public void ApplyPatch() {
            if (!PopupHost.CanCreatePopup()) return;
            PopupHost.ShowPopup(new Apply(this));
        }

        public void Cleanup() {
            if (!PopupHost.CanCreatePopup()) return;
            PopupHost.ShowAndStartPopup(new Cleanup(this));
        }

        public void OpenConfigure() {
            if (!PopupHost.CanCreatePopup()) return;
            PopupHost.ShowPopup(new RepositoryConfigure(this));
        }

        public void ClearSearchCommitFilter() {
            SearchCommitFilter = string.Empty;
        }

        public void StartSearchCommits() {
            if (_histories == null) return;

            var visible = new List<Models.Commit>();
            foreach (var c in _histories.Commits) {
                if (c.SHA.Contains(_searchCommitFilter, StringComparison.OrdinalIgnoreCase)
                    || c.Subject.Contains(_searchCommitFilter, StringComparison.OrdinalIgnoreCase)
                    || c.Message.Contains(_searchCommitFilter, StringComparison.OrdinalIgnoreCase)
                    || c.Author.Name.Contains(_searchCommitFilter, StringComparison.OrdinalIgnoreCase)
                    || c.Committer.Name.Contains(_searchCommitFilter, StringComparison.OrdinalIgnoreCase)) {
                    visible.Add(c);
                }
            }

            SearchedCommits = visible;
        }

        public void ExitSearchMode() {
            IsSearching = false;
        }

        public void SetWatcherEnabled(bool enabled) {
            if (_watcher != null) _watcher.SetEnabled(enabled);
        }

        public void NavigateToCommit(string sha) {
            if (_histories != null) {
                SelectedViewIndex = 0;
                _histories.NavigateTo(sha);
            }
        }

        public void UpdateFilter(string filter, bool toggle) {
            var changed = false;
            if (toggle) {
                if (!Filters.Contains(filter)) {
                    Filters.Add(filter);
                    changed = true;
                }
            } else {
                changed = Filters.Remove(filter);
            }

            if (changed) Task.Run(RefreshCommits);
        }

        public void StashAll() {
            if (PopupHost.CanCreatePopup()) {
                var changes = new List<Models.Change>();
                changes.AddRange(_workingCopy.Unstaged);
                changes.AddRange(_workingCopy.Staged);
                PopupHost.ShowPopup(new StashChanges(this, changes, true));
            }
        }

        public void GotoResolve() {
            if (_workingCopy != null) SelectedViewIndex = 1;
        }

        public async void ContinueMerge() {
            var cherryPickMerge = Path.Combine(_gitDir, "CHERRY_PICK_HEAD");
            var rebaseMerge = Path.Combine(_gitDir, "REBASE_HEAD");
            var rebaseMergeFolder = Path.Combine(_gitDir, "rebase-merge");
            var revertMerge = Path.Combine(_gitDir, "REVERT_HEAD");
            var otherMerge = Path.Combine(_gitDir, "MERGE_HEAD");

            var mode = "";
            if (File.Exists(cherryPickMerge)) {
                mode = "cherry-pick";
            } else if (File.Exists(rebaseMerge) && Directory.Exists(rebaseMergeFolder)) {
                mode = "rebase";
            } else if (File.Exists(revertMerge)) {
                mode = "revert";
            } else if (File.Exists(otherMerge)) {
                mode = "merge";
            } else {
                await Task.Run(RefreshWorkingCopyChanges);
                return;
            }

            var cmd = new Commands.Command();
            cmd.WorkingDirectory = _fullpath;
            cmd.Context = _fullpath;
            cmd.Args = $"-c core.editor=true {mode} --continue";

            SetWatcherEnabled(false);
            var succ = await Task.Run(cmd.Exec);
            SetWatcherEnabled(true);

            if (succ) {
                if (_workingCopy != null) _workingCopy.CommitMessage = string.Empty;

                if (mode == "rebase") {
                    if (File.Exists(rebaseMerge)) File.Delete(rebaseMerge);
                    if (Directory.Exists(rebaseMergeFolder)) Directory.Delete(rebaseMergeFolder);
                }
            }
        }

        public async void AbortMerge() {
            var cmd = new Commands.Command();
            cmd.WorkingDirectory = _fullpath;
            cmd.Context = _fullpath;

            if (File.Exists(Path.Combine(_gitDir, "CHERRY_PICK_HEAD"))) {
                cmd.Args = "cherry-pick --abort";
            } else if (File.Exists(Path.Combine(_gitDir, "REBASE_HEAD"))) {
                cmd.Args = "rebase --abort";
            } else if (File.Exists(Path.Combine(_gitDir, "REVERT_HEAD"))) {
                cmd.Args = "revert --abort";
            } else if (File.Exists(Path.Combine(_gitDir, "MERGE_HEAD"))) {
                cmd.Args = "merge --abort";
            } else {
                await Task.Run(RefreshWorkingCopyChanges);
                return;
            }

            SetWatcherEnabled(false);
            await Task.Run(cmd.Exec);
            SetWatcherEnabled(true);
        }

        public void RefreshBranches() {
            var branches = new Commands.QueryBranches(FullPath).Result();
            var remotes = new Commands.QueryRemotes(FullPath).Result();

            var builder = new Models.BranchTreeNode.Builder();
            builder.CollectExpandedNodes(_localBranchTrees, true);
            builder.CollectExpandedNodes(_remoteBranchTrees, false);
            builder.Run(branches, remotes);

            Dispatcher.UIThread.Invoke(() => {
                Remotes = remotes;
                Branches = branches;
                LocalBranchTrees = builder.Locals;
                RemoteBranchTrees = builder.Remotes;

                var cur = Branches.Find(x => x.IsCurrent);
                CanCommitWithPush = cur != null && !string.IsNullOrEmpty(cur.Upstream);
            });
        }

        public void RefreshTags() {
            var tags = new Commands.QueryTags(FullPath).Result();
            Dispatcher.UIThread.Invoke(() => {
                Tags = tags;
            });
        }

        public void RefreshCommits() {
            Dispatcher.UIThread.Invoke(() => _histories.IsLoading = true);

            var limits = $"-{Preference.Instance.MaxHistoryCommits} ";
            var validFilters = new List<string>();
            foreach (var filter in Filters) {
                if (filter.StartsWith("refs/")) {
                    if (_branches.FindIndex(x => x.FullName == filter) >= 0) validFilters.Add(filter);
                } else {
                    if (_tags.FindIndex(t => t.Name == filter) >= 0) validFilters.Add(filter);
                }
            }
            if (validFilters.Count > 0) {
                limits += string.Join(" ", validFilters);
            } else {
                limits += "--branches --remotes --tags";
            }

            var commits = new Commands.QueryCommits(FullPath, limits).Result();
            Dispatcher.UIThread.Invoke(() => {
                _histories.IsLoading = false;
                _histories.Commits = commits;
            });
        }

        public void RefreshSubmodules() {
            var submodules = new Commands.QuerySubmodules(FullPath).Result();
            Dispatcher.UIThread.Invoke(() => {
                Submodules = submodules;
            });
        }

        public void RefreshWorkingCopyChanges() {
            _watcher.MarkWorkingCopyRefreshed();

            var changes = new Commands.QueryLocalChanges(FullPath, _includeUntracked).Result();
            var hasUnsolvedConflict = _workingCopy.SetData(changes);

            var cherryPickMerge = Path.Combine(_gitDir, "CHERRY_PICK_HEAD");
            var rebaseMerge = Path.Combine(_gitDir, "REBASE_HEAD");
            var rebaseMergeFolder = Path.Combine(_gitDir, "rebase-merge");
            var revertMerge = Path.Combine(_gitDir, "REVERT_HEAD");
            var otherMerge = Path.Combine(_gitDir, "MERGE_HEAD");
            var runningMerge = (File.Exists(cherryPickMerge) ||
                (File.Exists(rebaseMerge) && Directory.Exists(rebaseMergeFolder)) ||
                File.Exists(revertMerge) ||
                File.Exists(otherMerge));

            if (!runningMerge) {
                if (Directory.Exists(rebaseMergeFolder)) Directory.Delete(rebaseMergeFolder, true);
                var applyFolder = Path.Combine(_gitDir, "rebase-apply");
                if (Directory.Exists(applyFolder)) Directory.Delete(applyFolder, true);
            }

            Dispatcher.UIThread.Invoke(() => {
                IsConflictBarVisible = runningMerge;
                HasUnsolvedConflict = hasUnsolvedConflict;
                OnPropertyChanged(nameof(WorkingCopyChangesCount));
            });
        }

        public void RefreshStashes() {
            var stashes = new Commands.QueryStashes(FullPath).Result();
            Dispatcher.UIThread.Invoke(() => {
                _stashesPage.Stashes = stashes;
                OnPropertyChanged(nameof(StashesCount));
            });
        }

        public void RefreshGitFlow() {
            var config = new Commands.Config(_fullpath).ListAll();
            var gitFlow = new Models.GitFlow();
            if (config.ContainsKey("gitflow.prefix.feature")) gitFlow.Feature = config["gitflow.prefix.feature"];
            if (config.ContainsKey("gitflow.prefix.release")) gitFlow.Release = config["gitflow.prefix.release"];
            if (config.ContainsKey("gitflow.prefix.hotfix")) gitFlow.Hotfix = config["gitflow.prefix.hotfix"];
            Dispatcher.UIThread.Invoke(() => {
                GitFlow = gitFlow;
            });
        }

        public void CreateNewBranch() {
            var current = Branches.Find(x => x.IsCurrent);
            if (current == null) {
                App.RaiseException(_fullpath, "Git do not hold any branch until you do first commit.");
                return;
            }

            if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new CreateBranch(this, current));
        }

        public void CreateNewTag() {
            var current = Branches.Find(x => x.IsCurrent);
            if (current == null) {
                App.RaiseException(_fullpath, "Git do not hold any branch until you do first commit.");
                return;
            }

            if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new CreateTag(this, current));
        }

        public void AddRemote() {
            if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new AddRemote(this));
        }

        public void AddSubmodule() {
            if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new AddSubmodule(this));
        }

        public ContextMenu CreateContextMenuForGitFlow() {
            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

            if (GitFlow.IsEnabled) {
                var startFeature = new MenuItem();
                startFeature.Header = App.Text("GitFlow.StartFeature");
                startFeature.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new GitFlowStart(this, Models.GitFlowBranchType.Feature));
                    e.Handled = true;
                };

                var startRelease = new MenuItem();
                startRelease.Header = App.Text("GitFlow.StartRelease");
                startRelease.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new GitFlowStart(this, Models.GitFlowBranchType.Release));
                    e.Handled = true;
                };

                var startHotfix = new MenuItem();
                startHotfix.Header = App.Text("GitFlow.StartHotfix");
                startHotfix.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new GitFlowStart(this, Models.GitFlowBranchType.Hotfix));
                    e.Handled = true;
                };

                menu.Items.Add(startFeature);
                menu.Items.Add(startRelease);
                menu.Items.Add(startHotfix);
            } else {
                var init = new MenuItem();
                init.Header = App.Text("GitFlow.Init");
                init.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new InitGitFlow(this));
                    e.Handled = true;
                };
                menu.Items.Add(init);
            }
            return menu;
        }

        public ContextMenu CreateContextMenuForLocalBranch(Models.Branch branch) {
            var menu = new ContextMenu();

            var push = new MenuItem();
            push.Header = App.Text("BranchCM.Push", branch.Name);
            push.Icon = CreateMenuIcon("Icons.Push");
            push.IsEnabled = Remotes.Count > 0;
            push.Click += (_, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Push(this, branch));
                e.Handled = true;
            };

            if (branch.IsCurrent) {
                var discard = new MenuItem();
                discard.Header = App.Text("BranchCM.DiscardAll");
                discard.Icon = CreateMenuIcon("Icons.Undo");
                discard.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Discard(this));
                    e.Handled = true;
                };

                menu.Items.Add(discard);
                menu.Items.Add(new MenuItem() { Header = "-" });

                if (!string.IsNullOrEmpty(branch.Upstream)) {
                    var upstream = branch.Upstream.Substring(13);
                    var fastForward = new MenuItem();
                    fastForward.Header = App.Text("BranchCM.FastForward", upstream);
                    fastForward.Icon = CreateMenuIcon("Icons.FastForward");
                    fastForward.IsEnabled = !string.IsNullOrEmpty(branch.UpstreamTrackStatus) && branch.UpstreamTrackStatus.IndexOf('↑') < 0;
                    fastForward.Click += (o, e) => {
                        if (PopupHost.CanCreatePopup()) PopupHost.ShowAndStartPopup(new Merge(this, upstream, branch.Name));
                        e.Handled = true;
                    };

                    var pull = new MenuItem();
                    pull.Header = App.Text("BranchCM.Pull", upstream);
                    pull.Icon = CreateMenuIcon("Icons.Pull");
                    pull.IsEnabled = !string.IsNullOrEmpty(branch.UpstreamTrackStatus);
                    pull.Click += (o, e) => {
                        if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Pull(this, null));
                        e.Handled = true;
                    };

                    menu.Items.Add(fastForward);
                    menu.Items.Add(pull);
                }

                menu.Items.Add(push);
            } else {
                var current = Branches.Find(x => x.IsCurrent);

                var checkout = new MenuItem();
                checkout.Header = App.Text("BranchCM.Checkout", branch.Name);
                checkout.Icon = CreateMenuIcon("Icons.Check");
                checkout.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowAndStartPopup(new Checkout(this, branch.Name));
                    e.Handled = true;
                };
                menu.Items.Add(checkout);

                var upstream = Branches.Find(x => x.FullName == branch.Upstream);
                if (upstream != null) {
                    var fastForward = new MenuItem();
                    fastForward.Header = App.Text("BranchCM.FastForward", $"{upstream.Remote}/{upstream.Name}");
                    fastForward.Icon = CreateMenuIcon("Icons.FastForward");
                    fastForward.IsEnabled = !string.IsNullOrEmpty(branch.UpstreamTrackStatus) && branch.UpstreamTrackStatus.IndexOf('↑') < 0;
                    fastForward.Click += (o, e) => {
                        if (PopupHost.CanCreatePopup()) PopupHost.ShowAndStartPopup(new FastForwardWithoutCheckout(this, branch, upstream));
                        e.Handled = true;
                    };

                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(fastForward);
                }

                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(push);

                var merge = new MenuItem();
                merge.Header = App.Text("BranchCM.Merge", branch.Name, current.Name);
                merge.Icon = CreateMenuIcon("Icons.Merge");
                merge.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Merge(this, branch.Name, current.Name));
                    e.Handled = true;
                };

                var rebase = new MenuItem();
                rebase.Header = App.Text("BranchCM.Rebase", current.Name, branch.Name);
                rebase.Icon = CreateMenuIcon("Icons.Rebase");
                rebase.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Rebase(this, current, branch));
                    e.Handled = true;
                };

                menu.Items.Add(merge);
                menu.Items.Add(rebase);
            }

            var type = GitFlow.GetBranchType(branch.Name);
            if (type != Models.GitFlowBranchType.None) {
                var finish = new MenuItem();
                finish.Header = App.Text("BranchCM.Finish", branch.Name);
                finish.Icon = CreateMenuIcon("Icons.Flow");
                finish.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new GitFlowFinish(this, branch, type));
                    e.Handled = true;
                };
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(finish);
            }

            var rename = new MenuItem();
            rename.Header = App.Text("BranchCM.Rename", branch.Name);
            rename.Icon = CreateMenuIcon("Icons.Rename");
            rename.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new RenameBranch(this, branch));
                e.Handled = true;
            };

            var delete = new MenuItem();
            delete.Header = App.Text("BranchCM.Delete", branch.Name);
            delete.Icon = CreateMenuIcon("Icons.Clear");
            delete.IsEnabled = !branch.IsCurrent;
            delete.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new DeleteBranch(this, branch));
                e.Handled = true;
            };

            var createBranch = new MenuItem();
            createBranch.Icon = CreateMenuIcon("Icons.Branch.Add");
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new CreateBranch(this, branch));
                e.Handled = true;
            };

            var createTag = new MenuItem();
            createTag.Icon = CreateMenuIcon("Icons.Tag.Add");
            createTag.Header = App.Text("CreateTag");
            createTag.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new CreateTag(this, branch));
                e.Handled = true;
            };

            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(rename);
            menu.Items.Add(delete);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(createBranch);
            menu.Items.Add(createTag);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var remoteBranches = new List<Models.Branch>();
            foreach (var b in Branches) {
                if (!b.IsLocal) remoteBranches.Add(b);
            }

            if (remoteBranches.Count > 0) {
                var tracking = new MenuItem();
                tracking.Header = App.Text("BranchCM.Tracking");
                tracking.Icon = CreateMenuIcon("Icons.Branch");

                foreach (var b in remoteBranches) {
                    var upstream = b.FullName.Replace("refs/remotes/", "");
                    var target = new MenuItem();
                    target.Header = upstream;
                    if (branch.Upstream == b.FullName) target.Icon = CreateMenuIcon("Icons.Check");

                    target.Click += (o, e) => {
                        if (Commands.Branch.SetUpstream(_fullpath, branch.Name, upstream)) {
                            Task.Run(RefreshBranches);
                        }
                        e.Handled = true;
                    };

                    tracking.Items.Add(target);
                }

                var unsetUpstream = new MenuItem();
                unsetUpstream.Header = App.Text("BranchCM.UnsetUpstream");
                unsetUpstream.Click += (_, e) => {
                    if (Commands.Branch.SetUpstream(_fullpath, branch.Name, string.Empty)) {
                        Task.Run(RefreshBranches);
                    }
                    e.Handled = true;
                };
                tracking.Items.Add(new MenuItem() { Header = "-" });
                tracking.Items.Add(unsetUpstream);

                menu.Items.Add(tracking);
            }

            var archive = new MenuItem();
            archive.Icon = CreateMenuIcon("Icons.Archive");
            archive.Header = App.Text("Archive");
            archive.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Archive(this, branch));
                e.Handled = true;
            };
            menu.Items.Add(archive);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = CreateMenuIcon("Icons.Copy");
            copy.Click += (o, e) => {
                App.CopyText(branch.Name);
                e.Handled = true;
            };
            menu.Items.Add(copy);

            return menu;
        }

        public ContextMenu CreateContextMenuForRemote(Models.Remote remote) {
            var menu = new ContextMenu();

            var fetch = new MenuItem();
            fetch.Header = App.Text("RemoteCM.Fetch");
            fetch.Icon = CreateMenuIcon("Icons.Fetch");
            fetch.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowAndStartPopup(new Fetch(this, remote));
                e.Handled = true;
            };

            var prune = new MenuItem();
            prune.Header = App.Text("RemoteCM.Prune");
            prune.Icon = CreateMenuIcon("Icons.Clear2");
            prune.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowAndStartPopup(new PruneRemote(this, remote));
                e.Handled = true;
            };

            var edit = new MenuItem();
            edit.Header = App.Text("RemoteCM.Edit");
            edit.Icon = CreateMenuIcon("Icons.Edit");
            edit.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new EditRemote(this, remote));
                e.Handled = true;
            };

            var delete = new MenuItem();
            delete.Header = App.Text("RemoteCM.Delete");
            delete.Icon = CreateMenuIcon("Icons.Clear");
            delete.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new DeleteRemote(this, remote));
                e.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("RemoteCM.CopyURL");
            copy.Icon = CreateMenuIcon("Icons.Copy");
            copy.Click += (o, e) => {
                App.CopyText(remote.URL);
                e.Handled = true;
            };

            menu.Items.Add(fetch);
            menu.Items.Add(prune);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(edit);
            menu.Items.Add(delete);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(copy);
            return menu;
        }

        public ContextMenu CreateContextMenuForRemoteBranch(Models.Branch branch) {
            var menu = new ContextMenu();
            var current = Branches.Find(x => x.IsCurrent);

            var checkout = new MenuItem();
            checkout.Header = App.Text("BranchCM.Checkout", $"{branch.Remote}/{branch.Name}");
            checkout.Icon = CreateMenuIcon("Icons.Check");
            checkout.Click += (o, e) => {
                foreach (var b in Branches) {
                    if (b.IsLocal && b.Upstream == branch.FullName) {
                        if (b.IsCurrent) return;
                        if (PopupHost.CanCreatePopup()) PopupHost.ShowAndStartPopup(new Checkout(this, b.Name));
                        return;
                    }
                }

                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new CreateBranch(this, branch));
                e.Handled = true;
            };
            menu.Items.Add(checkout);
            menu.Items.Add(new MenuItem() { Header = "-" });

            if (current != null) {
                var pull = new MenuItem();
                pull.Header = App.Text("BranchCM.PullInto", $"{branch.Remote}/{branch.Name}", current.Name);
                pull.Icon = CreateMenuIcon("Icons.Pull");
                pull.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Pull(this, branch));
                    e.Handled = true;
                };

                var merge = new MenuItem();
                merge.Header = App.Text("BranchCM.Merge", $"{branch.Remote}/{branch.Name}", current.Name);
                merge.Icon = CreateMenuIcon("Icons.Merge");
                merge.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Merge(this, $"{branch.Remote}/{branch.Name}", current.Name));
                    e.Handled = true;
                };

                var rebase = new MenuItem();
                rebase.Header = App.Text("BranchCM.Rebase", current.Name, $"{branch.Remote}/{branch.Name}");
                rebase.Icon = CreateMenuIcon("Icons.Rebase");
                rebase.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Rebase(this, current, branch));
                    e.Handled = true;
                };

                menu.Items.Add(pull);
                menu.Items.Add(merge);
                menu.Items.Add(rebase);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var delete = new MenuItem();
            delete.Header = App.Text("BranchCM.Delete", $"{branch.Remote}/{branch.Name}");
            delete.Icon = CreateMenuIcon("Icons.Clear");
            delete.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new DeleteBranch(this, branch));
                e.Handled = true;
            };

            var createBranch = new MenuItem();
            createBranch.Icon = CreateMenuIcon("Icons.Branch.Add");
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new CreateBranch(this, branch));
                e.Handled = true;
            };

            var createTag = new MenuItem();
            createTag.Icon = CreateMenuIcon("Icons.Tag.Add");
            createTag.Header = App.Text("CreateTag");
            createTag.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new CreateTag(this, branch));
                e.Handled = true;
            };

            var archive = new MenuItem();
            archive.Icon = CreateMenuIcon("Icons.Archive");
            archive.Header = App.Text("Archive");
            archive.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Archive(this, branch));
                e.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = CreateMenuIcon("Icons.Copy");
            copy.Click += (o, e) => {
                App.CopyText(branch.Remote + "/" + branch.Name);
                e.Handled = true;
            };

            menu.Items.Add(delete);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(createBranch);
            menu.Items.Add(createTag);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(archive);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(copy);
            return menu;
        }

        public ContextMenu CreateContextMenuForTag(Models.Tag tag) {
            var createBranch = new MenuItem();
            createBranch.Icon = CreateMenuIcon("Icons.Branch.Add");
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Click += (o, ev) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new CreateBranch(this, tag));
                ev.Handled = true;
            };

            var pushTag = new MenuItem();
            pushTag.Header = App.Text("TagCM.Push", tag.Name);
            pushTag.Icon = CreateMenuIcon("Icons.Push");
            pushTag.IsEnabled = Remotes.Count > 0;
            pushTag.Click += (o, ev) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new PushTag(this, tag));
                ev.Handled = true;
            };

            var deleteTag = new MenuItem();
            deleteTag.Header = App.Text("TagCM.Delete", tag.Name);
            deleteTag.Icon = CreateMenuIcon("Icons.Clear");
            deleteTag.Click += (o, ev) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new DeleteTag(this, tag));
                ev.Handled = true;
            };

            var archive = new MenuItem();
            archive.Icon = CreateMenuIcon("Icons.Archive");
            archive.Header = App.Text("Archive");
            archive.Click += (o, ev) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Archive(this, tag));
                ev.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("TagCM.Copy");
            copy.Icon = CreateMenuIcon("Icons.Copy");
            copy.Click += (o, ev) => {
                App.CopyText(tag.Name);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(createBranch);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(pushTag);
            menu.Items.Add(deleteTag);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(archive);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(copy);
            return menu;
        }

        public ContextMenu CreateContextMenuForSubmodule(string submodule) {
            var open = new MenuItem();
            open.Header = App.Text("Submodule.Open");
            open.Icon = CreateMenuIcon("Icons.Folder.Open");
            open.Click += (o, ev) => {
                var root = Path.GetFullPath(Path.Combine(_fullpath, submodule));
                var gitDir = new Commands.QueryGitDir(root).Result();
                var repo = Preference.AddRepository(root, gitDir);
                var node = new RepositoryNode() {
                    Id = root,
                    Name = Path.GetFileName(root),
                    Bookmark = 0,
                    IsRepository = true,
                };

                var launcher = App.GetTopLevel().DataContext as Launcher;
                if (launcher != null) {
                    launcher.OpenRepositoryInTab(node, null);
                }

                ev.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("Submodule.CopyPath");
            copy.Icon = CreateMenuIcon("Icons.Copy");
            copy.Click += (o, ev) => {
                App.CopyText(submodule);
                ev.Handled = true;
            };

            var rm = new MenuItem();
            rm.Header = App.Text("Submodule.Remove");
            rm.Icon = CreateMenuIcon("Icons.Clear");
            rm.Click += (o, ev) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new DeleteSubmodule(this, submodule));
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(open);
            menu.Items.Add(copy);
            menu.Items.Add(rm);
            return menu;
        }

        private Avalonia.Controls.Shapes.Path CreateMenuIcon(string key) {
            var icon = new Avalonia.Controls.Shapes.Path();
            icon.Width = 12;
            icon.Height = 12;
            icon.Stretch = Stretch.Uniform;
            icon.Data = App.Current?.FindResource(key) as StreamGeometry;
            return icon;
        }

        private string _fullpath = string.Empty;
        private string _gitDir = string.Empty;
        private Models.GitFlow _gitflow = new Models.GitFlow();

        private Models.Watcher _watcher = null;
        private Histories _histories = null;
        private WorkingCopy _workingCopy = null;
        private StashesPage _stashesPage = null;
        private int _selectedViewIndex = 0;
        private object _selectedView = null;

        private bool _isSearching = false;
        private string _searchCommitFilter = string.Empty;
        private List<Models.Commit> _searchedCommits = new List<Models.Commit>();

        private List<Models.Remote> _remotes = new List<Models.Remote>();
        private List<Models.Branch> _branches = new List<Models.Branch>();
        private List<Models.BranchTreeNode> _localBranchTrees = new List<Models.BranchTreeNode>();
        private List<Models.BranchTreeNode> _remoteBranchTrees = new List<Models.BranchTreeNode>();
        private List<Models.Tag> _tags = new List<Models.Tag>();
        private List<string> _submodules = new List<string>();
        private bool _isConflictBarVisible = false;
        private bool _hasUnsolvedConflict = false;
        private bool _canCommitWithPush = false;
        private bool _includeUntracked = true;
    }
}
