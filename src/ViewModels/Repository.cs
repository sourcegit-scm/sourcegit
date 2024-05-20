using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Repository : ObservableObject, Models.IRepository
    {
        public string FullPath
        {
            get => _fullpath;
            set
            {
                if (value != null)
                {
                    var normalized = value.Replace('\\', '/');
                    SetProperty(ref _fullpath, normalized);
                }
                else
                {
                    SetProperty(ref _fullpath, null);
                }
            }
        }

        public string GitDir
        {
            get => _gitDir;
            set => SetProperty(ref _gitDir, value);
        }

        public AvaloniaList<string> Filters
        {
            get;
            set;
        } = new AvaloniaList<string>();

        public AvaloniaList<string> CommitMessages
        {
            get;
            set;
        } = new AvaloniaList<string>();

        [JsonIgnore]
        public Models.GitFlow GitFlow
        {
            get => _gitflow;
            set => SetProperty(ref _gitflow, value);
        }

        [JsonIgnore]
        public int SelectedViewIndex
        {
            get => _selectedViewIndex;
            set
            {
                if (SetProperty(ref _selectedViewIndex, value))
                {
                    switch (value)
                    {
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
        public object SelectedView
        {
            get => _selectedView;
            set => SetProperty(ref _selectedView, value);
        }

        [JsonIgnore]
        public string SearchBranchFilter
        {
            get => _searchBranchFilter;
            set
            {
                if (SetProperty(ref _searchBranchFilter, value))
                {
                    var builder = BuildBranchTree(_branches, _remotes);
                    LocalBranchTrees = builder.Locals;
                    RemoteBranchTrees = builder.Remotes;
                }
            }
        }

        [JsonIgnore]
        public List<Models.Remote> Remotes
        {
            get => _remotes;
            private set => SetProperty(ref _remotes, value);
        }

        [JsonIgnore]
        public List<Models.Branch> Branches
        {
            get => _branches;
            private set => SetProperty(ref _branches, value);
        }

        [JsonIgnore]
        public List<BranchTreeNode> LocalBranchTrees
        {
            get => _localBranchTrees;
            private set => SetProperty(ref _localBranchTrees, value);
        }

        [JsonIgnore]
        public List<BranchTreeNode> RemoteBranchTrees
        {
            get => _remoteBranchTrees;
            private set => SetProperty(ref _remoteBranchTrees, value);
        }

        [JsonIgnore]
        public List<Models.Tag> Tags
        {
            get => _tags;
            private set => SetProperty(ref _tags, value);
        }

        [JsonIgnore]
        public List<string> Submodules
        {
            get => _submodules;
            private set => SetProperty(ref _submodules, value);
        }

        [JsonIgnore]
        public int WorkingCopyChangesCount
        {
            get => _workingCopy == null ? 0 : _workingCopy.Count;
        }

        [JsonIgnore]
        public int StashesCount
        {
            get => _stashesPage == null ? 0 : _stashesPage.Count;
        }

        [JsonIgnore]
        public bool CanCommitWithPush
        {
            get => _canCommitWithPush;
            private set => SetProperty(ref _canCommitWithPush, value);
        }

        [JsonIgnore]
        public bool IncludeUntracked
        {
            get => _includeUntracked;
            set
            {
                if (SetProperty(ref _includeUntracked, value))
                {
                    Task.Run(RefreshWorkingCopyChanges);
                }
            }
        }

        [JsonIgnore]
        public bool IsSearching
        {
            get => _isSearching;
            set
            {
                if (SetProperty(ref _isSearching, value))
                {
                    SearchedCommits = new List<Models.Commit>();
                    SearchCommitFilter = string.Empty;
                    if (value)
                        SelectedViewIndex = 0;
                }
            }
        }

        [JsonIgnore]
        public string SearchCommitFilter
        {
            get => _searchCommitFilter;
            set => SetProperty(ref _searchCommitFilter, value);
        }

        [JsonIgnore]
        public List<Models.Commit> SearchedCommits
        {
            get => _searchedCommits;
            set => SetProperty(ref _searchedCommits, value);
        }

        [JsonIgnore]
        public bool IsTagGroupExpanded
        {
            get => _isTagGroupExpanded;
            set => SetProperty(ref _isTagGroupExpanded, value);
        }

        [JsonIgnore]
        public bool IsSubmoduleGroupExpanded
        {
            get => _isSubmoduleGroupExpanded;
            set => SetProperty(ref _isSubmoduleGroupExpanded, value);
        }

        [JsonIgnore]
        public InProgressContext InProgressContext
        {
            get => _inProgressContext;
            private set => SetProperty(ref _inProgressContext, value);
        }

        [JsonIgnore]
        public bool HasUnsolvedConflicts
        {
            get => _hasUnsolvedConflicts;
            private set => SetProperty(ref _hasUnsolvedConflicts, value);
        }

        [JsonIgnore]
        public Models.Commit SearchResultSelectedCommit
        {
            get => _searchResultSelectedCommit;
            set => SetProperty(ref _searchResultSelectedCommit, value);
        }

        public void Open()
        {
            _watcher = new Models.Watcher(this);
            _histories = new Histories(this);
            _workingCopy = new WorkingCopy(this);
            _stashesPage = new StashesPage(this);
            _selectedView = _histories;
            _selectedViewIndex = 0;
            _inProgressContext = null;
            _hasUnsolvedConflicts = false;

            RefreshAll();
        }

        public void Close()
        {
            SelectedView = 0.0; // Do NOT modify. Used to remove exists widgets for GC.Collect

            _watcher.Dispose();
            _histories.Cleanup();
            _workingCopy.Cleanup();
            _stashesPage.Cleanup();

            _watcher = null;
            _histories = null;
            _workingCopy = null;
            _stashesPage = null;
            _isSearching = false;
            _searchCommitFilter = string.Empty;

            _isTagGroupExpanded = false;
            _isSubmoduleGroupExpanded = false;

            _inProgressContext = null;
            _hasUnsolvedConflicts = false;

            _remotes.Clear();
            _branches.Clear();
            _localBranchTrees.Clear();
            _remoteBranchTrees.Clear();
            _tags.Clear();
            _submodules.Clear();
            _searchedCommits.Clear();
        }

        public void RefreshAll()
        {
            Task.Run(() =>
            {
                RefreshBranches();
                RefreshTags();
                RefreshCommits();
            });

            Task.Run(RefreshSubmodules);
            Task.Run(RefreshWorkingCopyChanges);
            Task.Run(RefreshStashes);
            Task.Run(RefreshGitFlow);
        }

        public void OpenInFileManager()
        {
            Native.OS.OpenInFileManager(_fullpath);
        }

        public void OpenInTerminal()
        {
            Native.OS.OpenTerminal(_fullpath);
        }

        public ContextMenu CreateContextMenuForExternalTools()
        {
            var tools = Native.OS.ExternalTools;
            if (tools.Count == 0)
            {
                App.RaiseException(_fullpath, "No available external editors found!");
                return null;
            }

            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
            RenderOptions.SetBitmapInterpolationMode(menu, BitmapInterpolationMode.HighQuality);

            foreach (var tool in tools)
            {
                var dupTool = tool;

                var item = new MenuItem();
                item.Header = App.Text("Repository.OpenIn", dupTool.Name);
                item.Icon = new Image { Width = 16, Height = 16, Source = dupTool.IconImage };
                item.Click += (o, e) =>
                {
                    dupTool.Open(_fullpath);
                    e.Handled = true;
                };

                menu.Items.Add(item);
            }

            return menu;
        }

        public void Fetch()
        {
            if (!PopupHost.CanCreatePopup())
                return;

            if (Remotes.Count == 0)
            {
                App.RaiseException(_fullpath, "No remotes added to this repository!!!");
                return;
            }

            PopupHost.ShowPopup(new Fetch(this));
        }

        public void Pull()
        {
            if (!PopupHost.CanCreatePopup())
                return;

            if (Remotes.Count == 0)
            {
                App.RaiseException(_fullpath, "No remotes added to this repository!!!");
                return;
            }

            PopupHost.ShowPopup(new Pull(this, null));
        }

        public void Push()
        {
            if (!PopupHost.CanCreatePopup())
                return;

            if (Remotes.Count == 0)
            {
                App.RaiseException(_fullpath, "No remotes added to this repository!!!");
                return;
            }

            if (Branches.Find(x => x.IsCurrent) == null)
                App.RaiseException(_fullpath, "Can NOT found current branch!!!");
            PopupHost.ShowPopup(new Push(this, null));
        }

        public void ApplyPatch()
        {
            if (!PopupHost.CanCreatePopup())
                return;
            PopupHost.ShowPopup(new Apply(this));
        }

        public void Cleanup()
        {
            if (!PopupHost.CanCreatePopup())
                return;
            PopupHost.ShowAndStartPopup(new Cleanup(this));
        }

        public void OpenConfigure()
        {
            if (!PopupHost.CanCreatePopup())
                return;
            PopupHost.ShowPopup(new RepositoryConfigure(this));
        }

        public void ClearSearchCommitFilter()
        {
            SearchCommitFilter = string.Empty;
        }

        public void StartSearchCommits()
        {
            if (_histories == null)
                return;

            var visible = new List<Models.Commit>();
            foreach (var c in _histories.Commits)
            {
                if (c.SHA.Contains(_searchCommitFilter, StringComparison.OrdinalIgnoreCase)
                    || c.Subject.Contains(_searchCommitFilter, StringComparison.OrdinalIgnoreCase)
                    || c.Message.Contains(_searchCommitFilter, StringComparison.OrdinalIgnoreCase)
                    || c.Author.Name.Contains(_searchCommitFilter, StringComparison.OrdinalIgnoreCase)
                    || c.Committer.Name.Contains(_searchCommitFilter, StringComparison.OrdinalIgnoreCase)
                    || c.Author.Email.Contains(_searchCommitFilter, StringComparison.OrdinalIgnoreCase)
                    || c.Committer.Email.Contains(_searchCommitFilter, StringComparison.OrdinalIgnoreCase))
                {
                    visible.Add(c);
                }
            }

            SearchedCommits = visible;
        }

        public void ClearSearchBranchFilter()
        {
            SearchBranchFilter = string.Empty;
        }

        public void SetWatcherEnabled(bool enabled)
        {
            if (_watcher != null)
                _watcher.SetEnabled(enabled);
        }

        public void MarkBranchesDirtyManually()
        {
            if (_watcher != null)
                _watcher.MarkBranchDirtyManually();
        }

        public void MarkWorkingCopyDirtyManually()
        {
            if (_watcher != null)
                _watcher.MarkWorkingCopyDirtyManually();
        }

        public void NavigateToCommit(string sha)
        {
            if (_histories != null)
            {
                SelectedViewIndex = 0;
                _histories.NavigateTo(sha);
            }
        }

        public void NavigateToCurrentHead()
        {
            var cur = Branches.Find(x => x.IsCurrent);
            if (cur != null)
                NavigateToCommit(cur.Head);
        }

        public void UpdateFilter(string filter, bool toggle)
        {
            var changed = false;
            if (toggle)
            {
                if (!Filters.Contains(filter))
                {
                    Filters.Add(filter);
                    changed = true;
                }
            }
            else
            {
                changed = Filters.Remove(filter);
            }

            if (changed)
                Task.Run(RefreshCommits);
        }

        public void StashAll()
        {
            if (PopupHost.CanCreatePopup())
            {
                var changes = new List<Models.Change>();
                changes.AddRange(_workingCopy.Unstaged);
                changes.AddRange(_workingCopy.Staged);
                PopupHost.ShowPopup(new StashChanges(this, changes, true));
            }
        }

        public void GotoResolve()
        {
            if (_workingCopy != null)
                SelectedViewIndex = 1;
        }

        public async void ContinueMerge()
        {
            if (_inProgressContext != null)
            {
                SetWatcherEnabled(false);
                var succ = await Task.Run(_inProgressContext.Continue);
                if (succ && _workingCopy != null)
                {
                    _workingCopy.CommitMessage = string.Empty;
                }
                SetWatcherEnabled(true);
            }
            else
            {
                MarkWorkingCopyDirtyManually();
            }
        }

        public async void AbortMerge()
        {
            if (_inProgressContext != null)
            {
                SetWatcherEnabled(false);
                var succ = await Task.Run(_inProgressContext.Abort);
                if (succ && _workingCopy != null)
                {
                    _workingCopy.CommitMessage = string.Empty;
                }
                SetWatcherEnabled(true);
            }
            else
            {
                MarkWorkingCopyDirtyManually();
            }
        }

        public void RefreshBranches()
        {
            var branches = new Commands.QueryBranches(FullPath).Result();
            var remotes = new Commands.QueryRemotes(FullPath).Result();
            var builder = BuildBranchTree(branches, remotes);

            Dispatcher.UIThread.Invoke(() =>
            {
                Remotes = remotes;
                Branches = branches;
                LocalBranchTrees = builder.Locals;
                RemoteBranchTrees = builder.Remotes;

                var cur = Branches.Find(x => x.IsCurrent);
                CanCommitWithPush = cur != null && !string.IsNullOrEmpty(cur.Upstream);
            });
        }

        public void RefreshTags()
        {
            var tags = new Commands.QueryTags(FullPath).Result();
            foreach (var tag in tags)
                tag.IsFiltered = Filters.Contains(tag.Name);
            Dispatcher.UIThread.Invoke(() =>
            {
                Tags = tags;
            });
        }

        public void RefreshCommits()
        {
            Dispatcher.UIThread.Invoke(() => _histories.IsLoading = true);

            var limits = $"-{Preference.Instance.MaxHistoryCommits} ";
            var validFilters = new List<string>();
            foreach (var filter in Filters)
            {
                if (filter.StartsWith("refs/", StringComparison.Ordinal))
                {
                    if (_branches.FindIndex(x => x.FullName == filter) >= 0)
                        validFilters.Add(filter);
                }
                else
                {
                    if (_tags.FindIndex(t => t.Name == filter) >= 0)
                        validFilters.Add(filter);
                }
            }
            if (validFilters.Count > 0)
            {
                limits += string.Join(" ", validFilters);
            }
            else
            {
                limits += "--branches --remotes --tags";
            }

            var commits = new Commands.QueryCommits(FullPath, limits).Result();
            Dispatcher.UIThread.Invoke(() =>
            {
                if (_histories != null)
                {
                    _histories.IsLoading = false;
                    _histories.Commits = commits;
                }
            });
        }

        public void RefreshSubmodules()
        {
            var submodules = new Commands.QuerySubmodules(FullPath).Result();
            Dispatcher.UIThread.Invoke(() =>
            {
                Submodules = submodules;
            });
        }

        public void RefreshWorkingCopyChanges()
        {
            var changes = new Commands.QueryLocalChanges(FullPath, _includeUntracked).Result();
            if (_workingCopy == null)
                return;

            var hasUnsolvedConflict = _workingCopy.SetData(changes);
            var inProgress = null as InProgressContext;

            var rebaseMergeFolder = Path.Combine(_gitDir, "rebase-merge");
            var rebaseApplyFolder = Path.Combine(_gitDir, "rebase-apply");
            if (File.Exists(Path.Combine(_gitDir, "CHERRY_PICK_HEAD")))
            {
                inProgress = new CherryPickInProgress(_fullpath);
            }
            else if (File.Exists(Path.Combine(_gitDir, "REBASE_HEAD")) && Directory.Exists(rebaseMergeFolder))
            {
                inProgress = new RebaseInProgress(this);
            }
            else if (File.Exists(Path.Combine(_gitDir, "REVERT_HEAD")))
            {
                inProgress = new RevertInProgress(_fullpath);
            }
            else if (File.Exists(Path.Combine(_gitDir, "MERGE_HEAD")))
            {
                inProgress = new MergeInProgress(_fullpath);
            }
            else
            {
                if (Directory.Exists(rebaseMergeFolder))
                    Directory.Delete(rebaseMergeFolder, true);

                if (Directory.Exists(rebaseApplyFolder))
                    Directory.Delete(rebaseApplyFolder, true);
            }

            Dispatcher.UIThread.Invoke(() =>
            {
                InProgressContext = inProgress;
                HasUnsolvedConflicts = hasUnsolvedConflict;
                OnPropertyChanged(nameof(WorkingCopyChangesCount));
            });
        }

        public void RefreshStashes()
        {
            var stashes = new Commands.QueryStashes(FullPath).Result();
            Dispatcher.UIThread.Invoke(() =>
            {
                if (_stashesPage != null)
                    _stashesPage.Stashes = stashes;
                OnPropertyChanged(nameof(StashesCount));
            });
        }

        public void RefreshGitFlow()
        {
            var config = new Commands.Config(_fullpath).ListAll();
            var gitFlow = new Models.GitFlow();
            if (config.TryGetValue("gitflow.prefix.feature", out var feature))
                gitFlow.Feature = feature;
            if (config.TryGetValue("gitflow.prefix.release", out var release))
                gitFlow.Release = release;
            if (config.TryGetValue("gitflow.prefix.hotfix", out var hotfix))
                gitFlow.Hotfix = hotfix;
            Dispatcher.UIThread.Invoke(() =>
            {
                GitFlow = gitFlow;
            });
        }

        public void CreateNewBranch()
        {
            var current = Branches.Find(x => x.IsCurrent);
            if (current == null)
            {
                App.RaiseException(_fullpath, "Git do not hold any branch until you do first commit.");
                return;
            }

            if (PopupHost.CanCreatePopup())
                PopupHost.ShowPopup(new CreateBranch(this, current));
        }

        public void CheckoutLocalBranch(string branch)
        {
            if (!PopupHost.CanCreatePopup())
                return;

            if (WorkingCopyChangesCount > 0)
                PopupHost.ShowPopup(new Checkout(this, branch));
            else
                PopupHost.ShowAndStartPopup(new Checkout(this, branch));
        }

        public void CreateNewTag()
        {
            var current = Branches.Find(x => x.IsCurrent);
            if (current == null)
            {
                App.RaiseException(_fullpath, "Git do not hold any branch until you do first commit.");
                return;
            }

            if (PopupHost.CanCreatePopup())
                PopupHost.ShowPopup(new CreateTag(this, current));
        }

        public void AddRemote()
        {
            if (PopupHost.CanCreatePopup())
                PopupHost.ShowPopup(new AddRemote(this));
        }

        public void AddSubmodule()
        {
            if (PopupHost.CanCreatePopup())
                PopupHost.ShowPopup(new AddSubmodule(this));
        }

        public ContextMenu CreateContextMenuForGitFlow()
        {
            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

            if (GitFlow.IsEnabled)
            {
                var startFeature = new MenuItem();
                startFeature.Header = App.Text("GitFlow.StartFeature");
                startFeature.Icon = App.CreateMenuIcon("Icons.GitFlow.Feature");
                startFeature.Click += (o, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new GitFlowStart(this, Models.GitFlowBranchType.Feature));
                    e.Handled = true;
                };

                var startRelease = new MenuItem();
                startRelease.Header = App.Text("GitFlow.StartRelease");
                startRelease.Icon = App.CreateMenuIcon("Icons.GitFlow.Release");
                startRelease.Click += (o, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new GitFlowStart(this, Models.GitFlowBranchType.Release));
                    e.Handled = true;
                };

                var startHotfix = new MenuItem();
                startHotfix.Header = App.Text("GitFlow.StartHotfix");
                startHotfix.Icon = App.CreateMenuIcon("Icons.GitFlow.Hotfix");
                startHotfix.Click += (o, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new GitFlowStart(this, Models.GitFlowBranchType.Hotfix));
                    e.Handled = true;
                };

                menu.Items.Add(startFeature);
                menu.Items.Add(startRelease);
                menu.Items.Add(startHotfix);
            }
            else
            {
                var init = new MenuItem();
                init.Header = App.Text("GitFlow.Init");
                init.Icon = App.CreateMenuIcon("Icons.GitFlow.Init");
                init.Click += (o, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new InitGitFlow(this));
                    e.Handled = true;
                };
                menu.Items.Add(init);
            }
            return menu;
        }

        public ContextMenu CreateContextMenuForLocalBranch(Models.Branch branch)
        {
            var menu = new ContextMenu();

            var push = new MenuItem();
            push.Header = new Views.NameHighlightedTextBlock("BranchCM.Push", branch.Name);
            push.Icon = App.CreateMenuIcon("Icons.Push");
            push.IsEnabled = Remotes.Count > 0;
            push.Click += (_, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new Push(this, branch));
                e.Handled = true;
            };

            if (branch.IsCurrent)
            {
                var discard = new MenuItem();
                discard.Header = App.Text("BranchCM.DiscardAll");
                discard.Icon = App.CreateMenuIcon("Icons.Undo");
                discard.IsEnabled = _workingCopy.Count > 0;
                discard.Click += (o, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new Discard(this));
                    e.Handled = true;
                };

                menu.Items.Add(discard);
                menu.Items.Add(new MenuItem() { Header = "-" });

                if (!string.IsNullOrEmpty(branch.Upstream))
                {
                    var upstream = branch.Upstream.Substring(13);
                    var fastForward = new MenuItem();
                    fastForward.Header = new Views.NameHighlightedTextBlock("BranchCM.FastForward", upstream);
                    fastForward.Icon = App.CreateMenuIcon("Icons.FastForward");
                    fastForward.IsEnabled = !string.IsNullOrEmpty(branch.UpstreamTrackStatus) && branch.UpstreamTrackStatus.IndexOf('↑') < 0;
                    fastForward.Click += (o, e) =>
                    {
                        if (PopupHost.CanCreatePopup())
                            PopupHost.ShowAndStartPopup(new Merge(this, upstream, branch.Name));
                        e.Handled = true;
                    };

                    var pull = new MenuItem();
                    pull.Header = new Views.NameHighlightedTextBlock("BranchCM.Pull", upstream);
                    pull.Icon = App.CreateMenuIcon("Icons.Pull");
                    pull.Click += (o, e) =>
                    {
                        if (PopupHost.CanCreatePopup())
                            PopupHost.ShowPopup(new Pull(this, null));
                        e.Handled = true;
                    };

                    menu.Items.Add(fastForward);
                    menu.Items.Add(pull);
                }

                menu.Items.Add(push);
            }
            else
            {
                var current = Branches.Find(x => x.IsCurrent);

                var checkout = new MenuItem();
                checkout.Header = new Views.NameHighlightedTextBlock("BranchCM.Checkout", branch.Name);
                checkout.Icon = App.CreateMenuIcon("Icons.Check");
                checkout.Click += (o, e) =>
                {
                    CheckoutLocalBranch(branch.Name);
                    e.Handled = true;
                };
                menu.Items.Add(checkout);

                var upstream = Branches.Find(x => x.FullName == branch.Upstream);
                if (upstream != null)
                {
                    var fastForward = new MenuItem();
                    fastForward.Header = new Views.NameHighlightedTextBlock("BranchCM.FastForward", $"{upstream.Remote}/{upstream.Name}");
                    fastForward.Icon = App.CreateMenuIcon("Icons.FastForward");
                    fastForward.IsEnabled = !string.IsNullOrEmpty(branch.UpstreamTrackStatus) && branch.UpstreamTrackStatus.IndexOf('↑') < 0;
                    fastForward.Click += (o, e) =>
                    {
                        if (PopupHost.CanCreatePopup())
                            PopupHost.ShowAndStartPopup(new FastForwardWithoutCheckout(this, branch, upstream));
                        e.Handled = true;
                    };

                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(fastForward);
                }

                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(push);

                var merge = new MenuItem();
                merge.Header = new Views.NameHighlightedTextBlock("BranchCM.Merge", branch.Name, current.Name);
                merge.Icon = App.CreateMenuIcon("Icons.Merge");
                merge.Click += (o, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new Merge(this, branch.Name, current.Name));
                    e.Handled = true;
                };

                var rebase = new MenuItem();
                rebase.Header = new Views.NameHighlightedTextBlock("BranchCM.Rebase", current.Name, branch.Name);
                rebase.Icon = App.CreateMenuIcon("Icons.Rebase");
                rebase.Click += (o, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new Rebase(this, current, branch));
                    e.Handled = true;
                };

                menu.Items.Add(merge);
                menu.Items.Add(rebase);
            }

            var type = GitFlow.GetBranchType(branch.Name);
            if (type != Models.GitFlowBranchType.None)
            {
                var finish = new MenuItem();
                finish.Header = new Views.NameHighlightedTextBlock("BranchCM.Finish", branch.Name);
                finish.Icon = App.CreateMenuIcon("Icons.Flow");
                finish.Click += (o, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new GitFlowFinish(this, branch, type));
                    e.Handled = true;
                };
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(finish);
            }

            var rename = new MenuItem();
            rename.Header = new Views.NameHighlightedTextBlock("BranchCM.Rename", branch.Name);
            rename.Icon = App.CreateMenuIcon("Icons.Rename");
            rename.Click += (o, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new RenameBranch(this, branch));
                e.Handled = true;
            };

            var delete = new MenuItem();
            delete.Header = new Views.NameHighlightedTextBlock("BranchCM.Delete", branch.Name);
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
            delete.IsEnabled = !branch.IsCurrent;
            delete.Click += (o, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new DeleteBranch(this, branch));
                e.Handled = true;
            };

            var createBranch = new MenuItem();
            createBranch.Icon = App.CreateMenuIcon("Icons.Branch.Add");
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Click += (o, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new CreateBranch(this, branch));
                e.Handled = true;
            };

            var createTag = new MenuItem();
            createTag.Icon = App.CreateMenuIcon("Icons.Tag.Add");
            createTag.Header = App.Text("CreateTag");
            createTag.Click += (o, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new CreateTag(this, branch));
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
            foreach (var b in Branches)
            {
                if (!b.IsLocal)
                    remoteBranches.Add(b);
            }

            if (remoteBranches.Count > 0)
            {
                var tracking = new MenuItem();
                tracking.Header = App.Text("BranchCM.Tracking");
                tracking.Icon = App.CreateMenuIcon("Icons.Branch");

                foreach (var b in remoteBranches)
                {
                    var upstream = b.FullName.Replace("refs/remotes/", "");
                    var target = new MenuItem();
                    target.Header = upstream;
                    if (branch.Upstream == b.FullName)
                        target.Icon = App.CreateMenuIcon("Icons.Check");

                    target.Click += (o, e) =>
                    {
                        if (Commands.Branch.SetUpstream(_fullpath, branch.Name, upstream))
                        {
                            Task.Run(RefreshBranches);
                        }
                        e.Handled = true;
                    };

                    tracking.Items.Add(target);
                }

                var unsetUpstream = new MenuItem();
                unsetUpstream.Header = App.Text("BranchCM.UnsetUpstream");
                unsetUpstream.Click += (_, e) =>
                {
                    if (Commands.Branch.SetUpstream(_fullpath, branch.Name, string.Empty))
                    {
                        Task.Run(RefreshBranches);
                    }
                    e.Handled = true;
                };
                tracking.Items.Add(new MenuItem() { Header = "-" });
                tracking.Items.Add(unsetUpstream);

                menu.Items.Add(tracking);
            }

            var archive = new MenuItem();
            archive.Icon = App.CreateMenuIcon("Icons.Archive");
            archive.Header = App.Text("Archive");
            archive.Click += (o, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new Archive(this, branch));
                e.Handled = true;
            };
            menu.Items.Add(archive);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (o, e) =>
            {
                App.CopyText(branch.Name);
                e.Handled = true;
            };
            menu.Items.Add(copy);

            return menu;
        }

        public ContextMenu CreateContextMenuForRemote(Models.Remote remote)
        {
            var menu = new ContextMenu();

            var fetch = new MenuItem();
            fetch.Header = App.Text("RemoteCM.Fetch");
            fetch.Icon = App.CreateMenuIcon("Icons.Fetch");
            fetch.Click += (o, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowAndStartPopup(new Fetch(this, remote));
                e.Handled = true;
            };

            var prune = new MenuItem();
            prune.Header = App.Text("RemoteCM.Prune");
            prune.Icon = App.CreateMenuIcon("Icons.Clear2");
            prune.Click += (o, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowAndStartPopup(new PruneRemote(this, remote));
                e.Handled = true;
            };

            var edit = new MenuItem();
            edit.Header = App.Text("RemoteCM.Edit");
            edit.Icon = App.CreateMenuIcon("Icons.Edit");
            edit.Click += (o, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new EditRemote(this, remote));
                e.Handled = true;
            };

            var delete = new MenuItem();
            delete.Header = App.Text("RemoteCM.Delete");
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
            delete.Click += (o, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new DeleteRemote(this, remote));
                e.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("RemoteCM.CopyURL");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (o, e) =>
            {
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

        public ContextMenu CreateContextMenuForRemoteBranch(Models.Branch branch)
        {
            var menu = new ContextMenu();
            var current = Branches.Find(x => x.IsCurrent);

            var checkout = new MenuItem();
            checkout.Header = new Views.NameHighlightedTextBlock("BranchCM.Checkout", $"{branch.Remote}/{branch.Name}");
            checkout.Icon = App.CreateMenuIcon("Icons.Check");
            checkout.Click += (o, e) =>
            {
                foreach (var b in Branches)
                {
                    if (b.IsLocal && b.Upstream == branch.FullName)
                    {
                        if (b.IsCurrent)
                            return;
                        if (PopupHost.CanCreatePopup())
                            PopupHost.ShowAndStartPopup(new Checkout(this, b.Name));
                        return;
                    }
                }

                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new CreateBranch(this, branch));
                e.Handled = true;
            };
            menu.Items.Add(checkout);
            menu.Items.Add(new MenuItem() { Header = "-" });

            if (current != null)
            {
                var pull = new MenuItem();
                pull.Header = new Views.NameHighlightedTextBlock("BranchCM.PullInto", $"{branch.Remote}/{branch.Name}", current.Name);
                pull.Icon = App.CreateMenuIcon("Icons.Pull");
                pull.Click += (o, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new Pull(this, branch));
                    e.Handled = true;
                };

                var merge = new MenuItem();
                merge.Header = new Views.NameHighlightedTextBlock("BranchCM.Merge", $"{branch.Remote}/{branch.Name}", current.Name);
                merge.Icon = App.CreateMenuIcon("Icons.Merge");
                merge.Click += (o, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new Merge(this, $"{branch.Remote}/{branch.Name}", current.Name));
                    e.Handled = true;
                };

                var rebase = new MenuItem();
                rebase.Header = new Views.NameHighlightedTextBlock("BranchCM.Rebase", current.Name, $"{branch.Remote}/{branch.Name}");
                rebase.Icon = App.CreateMenuIcon("Icons.Rebase");
                rebase.Click += (o, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new Rebase(this, current, branch));
                    e.Handled = true;
                };

                menu.Items.Add(pull);
                menu.Items.Add(merge);
                menu.Items.Add(rebase);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var delete = new MenuItem();
            delete.Header = new Views.NameHighlightedTextBlock("BranchCM.Delete", $"{branch.Remote}/{branch.Name}");
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
            delete.Click += (o, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new DeleteBranch(this, branch));
                e.Handled = true;
            };

            var createBranch = new MenuItem();
            createBranch.Icon = App.CreateMenuIcon("Icons.Branch.Add");
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Click += (o, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new CreateBranch(this, branch));
                e.Handled = true;
            };

            var createTag = new MenuItem();
            createTag.Icon = App.CreateMenuIcon("Icons.Tag.Add");
            createTag.Header = App.Text("CreateTag");
            createTag.Click += (o, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new CreateTag(this, branch));
                e.Handled = true;
            };

            var archive = new MenuItem();
            archive.Icon = App.CreateMenuIcon("Icons.Archive");
            archive.Header = App.Text("Archive");
            archive.Click += (o, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new Archive(this, branch));
                e.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (o, e) =>
            {
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

        public ContextMenu CreateContextMenuForTag(Models.Tag tag)
        {
            var createBranch = new MenuItem();
            createBranch.Icon = App.CreateMenuIcon("Icons.Branch.Add");
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Click += (o, ev) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new CreateBranch(this, tag));
                ev.Handled = true;
            };

            var pushTag = new MenuItem();
            pushTag.Header = new Views.NameHighlightedTextBlock("TagCM.Push", tag.Name);
            pushTag.Icon = App.CreateMenuIcon("Icons.Push");
            pushTag.IsEnabled = Remotes.Count > 0;
            pushTag.Click += (o, ev) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new PushTag(this, tag));
                ev.Handled = true;
            };

            var deleteTag = new MenuItem();
            deleteTag.Header = new Views.NameHighlightedTextBlock("TagCM.Delete", tag.Name);
            deleteTag.Icon = App.CreateMenuIcon("Icons.Clear");
            deleteTag.Click += (o, ev) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new DeleteTag(this, tag));
                ev.Handled = true;
            };

            var archive = new MenuItem();
            archive.Icon = App.CreateMenuIcon("Icons.Archive");
            archive.Header = App.Text("Archive");
            archive.Click += (o, ev) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new Archive(this, tag));
                ev.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("TagCM.Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (o, ev) =>
            {
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

        public ContextMenu CreateContextMenuForSubmodule(string submodule)
        {
            var open = new MenuItem();
            open.Header = App.Text("Submodule.Open");
            open.Icon = App.CreateMenuIcon("Icons.Folder.Open");
            open.Click += (o, ev) =>
            {
                var root = Path.GetFullPath(Path.Combine(_fullpath, submodule));
                var gitDir = new Commands.QueryGitDir(root).Result();
                var repo = Preference.AddRepository(root, gitDir);
                var node = new RepositoryNode()
                {
                    Id = repo.FullPath,
                    Name = Path.GetFileName(repo.FullPath),
                    Bookmark = 0,
                    IsRepository = true,
                };

                var launcher = App.GetTopLevel().DataContext as Launcher;
                if (launcher != null)
                {
                    launcher.OpenRepositoryInTab(node, null);
                }

                ev.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("Submodule.CopyPath");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (o, ev) =>
            {
                App.CopyText(submodule);
                ev.Handled = true;
            };

            var rm = new MenuItem();
            rm.Header = App.Text("Submodule.Remove");
            rm.Icon = App.CreateMenuIcon("Icons.Clear");
            rm.Click += (o, ev) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new DeleteSubmodule(this, submodule));
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(open);
            menu.Items.Add(copy);
            menu.Items.Add(rm);
            return menu;
        }

        private BranchTreeNode.Builder BuildBranchTree(List<Models.Branch> branches, List<Models.Remote> remotes)
        {
            var builder = new BranchTreeNode.Builder();
            builder.SetFilters(Filters);

            if (string.IsNullOrEmpty(_searchBranchFilter))
            {
                builder.CollectExpandedNodes(_localBranchTrees, true);
                builder.CollectExpandedNodes(_remoteBranchTrees, false);
                builder.Run(branches, remotes, false);
            }
            else
            {
                var visibles = new List<Models.Branch>();
                foreach (var b in branches)
                {
                    if (b.FullName.Contains(_searchBranchFilter, StringComparison.OrdinalIgnoreCase))
                        visibles.Add(b);
                }

                builder.Run(visibles, remotes, visibles.Count <= 20);
            }

            return builder;
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

        private bool _isTagGroupExpanded = false;
        private bool _isSubmoduleGroupExpanded = false;

        private string _searchBranchFilter = string.Empty;

        private List<Models.Remote> _remotes = new List<Models.Remote>();
        private List<Models.Branch> _branches = new List<Models.Branch>();
        private List<BranchTreeNode> _localBranchTrees = new List<BranchTreeNode>();
        private List<BranchTreeNode> _remoteBranchTrees = new List<BranchTreeNode>();
        private List<Models.Tag> _tags = new List<Models.Tag>();
        private List<string> _submodules = new List<string>();
        private bool _canCommitWithPush = false;
        private bool _includeUntracked = true;

        private InProgressContext _inProgressContext = null;
        private bool _hasUnsolvedConflicts = false;
        private Models.Commit _searchResultSelectedCommit = null;
    }
}
