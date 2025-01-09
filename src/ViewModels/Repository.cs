using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
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

        public Models.RepositorySettings Settings
        {
            get => _settings;
        }

        public Models.FilterMode HistoriesFilterMode
        {
            get => _historiesFilterMode;
            private set => SetProperty(ref _historiesFilterMode, value);
        }

        public bool HasAllowedSignersFile
        {
            get => _hasAllowedSignersFile;
        }

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

        public object SelectedView
        {
            get => _selectedView;
            set => SetProperty(ref _selectedView, value);
        }

        public bool EnableReflog
        {
            get => _settings.EnableReflog;
            set
            {
                if (value != _settings.EnableReflog)
                {
                    _settings.EnableReflog = value;
                    OnPropertyChanged();
                    Task.Run(RefreshCommits);
                }
            }
        }

        public bool EnableFirstParentInHistories
        {
            get => _settings.EnableFirstParentInHistories;
            set
            {
                if (value != _settings.EnableFirstParentInHistories)
                {
                    _settings.EnableFirstParentInHistories = value;
                    OnPropertyChanged();
                    Task.Run(RefreshCommits);
                }
            }
        }

        public bool EnableTopoOrderInHistories
        {
            get => _settings.EnableTopoOrderInHistories;
            set
            {
                if (value != _settings.EnableTopoOrderInHistories)
                {
                    _settings.EnableTopoOrderInHistories = value;
                    OnPropertyChanged();
                    Task.Run(RefreshCommits);
                }
            }
        }

        public bool OnlyHighlightCurrentBranchInHistories
        {
            get => _settings.OnlyHighlighCurrentBranchInHistories;
            set
            {
                if (value != _settings.OnlyHighlighCurrentBranchInHistories)
                {
                    _settings.OnlyHighlighCurrentBranchInHistories = value;
                    OnPropertyChanged();
                }
            }
        }

        public Models.TagSortMode TagSortMode
        {
            get => _settings.TagSortMode;
            set
            {
                if (value != _settings.TagSortMode)
                {
                    _settings.TagSortMode = value;
                    OnPropertyChanged();
                    VisibleTags = BuildVisibleTags();
                }
            }
        }

        public string Filter
        {
            get => _filter;
            set
            {
                if (SetProperty(ref _filter, value))
                {
                    var builder = BuildBranchTree(_branches, _remotes);
                    LocalBranchTrees = builder.Locals;
                    RemoteBranchTrees = builder.Remotes;
                    VisibleTags = BuildVisibleTags();
                    VisibleSubmodules = BuildVisibleSubmodules();
                }
            }
        }

        public List<Models.Remote> Remotes
        {
            get => _remotes;
            private set => SetProperty(ref _remotes, value);
        }

        public List<Models.Branch> Branches
        {
            get => _branches;
            private set => SetProperty(ref _branches, value);
        }

        public Models.Branch CurrentBranch
        {
            get => _currentBranch;
            private set => SetProperty(ref _currentBranch, value);
        }

        public List<BranchTreeNode> LocalBranchTrees
        {
            get => _localBranchTrees;
            private set => SetProperty(ref _localBranchTrees, value);
        }

        public List<BranchTreeNode> RemoteBranchTrees
        {
            get => _remoteBranchTrees;
            private set => SetProperty(ref _remoteBranchTrees, value);
        }

        public List<Models.Worktree> Worktrees
        {
            get => _worktrees;
            private set => SetProperty(ref _worktrees, value);
        }

        public List<Models.Tag> Tags
        {
            get => _tags;
            private set => SetProperty(ref _tags, value);
        }

        public List<Models.Tag> VisibleTags
        {
            get => _visibleTags;
            private set => SetProperty(ref _visibleTags, value);
        }

        public List<Models.Submodule> Submodules
        {
            get => _submodules;
            private set => SetProperty(ref _submodules, value);
        }

        public List<Models.Submodule> VisibleSubmodules
        {
            get => _visibleSubmodules;
            private set => SetProperty(ref _visibleSubmodules, value);
        }

        public int LocalChangesCount
        {
            get => _localChangesCount;
            private set => SetProperty(ref _localChangesCount, value);
        }

        public int StashesCount
        {
            get => _stashesCount;
            private set => SetProperty(ref _stashesCount, value);
        }

        public bool IncludeUntracked
        {
            get => _settings.IncludeUntrackedInLocalChanges;
            set
            {
                if (value != _settings.IncludeUntrackedInLocalChanges)
                {
                    _settings.IncludeUntrackedInLocalChanges = value;
                    OnPropertyChanged();
                    Task.Run(RefreshWorkingCopyChanges);
                }
            }
        }

        public bool IsSearching
        {
            get => _isSearching;
            set
            {
                if (SetProperty(ref _isSearching, value))
                {
                    SearchedCommits = new List<Models.Commit>();
                    SearchCommitFilter = string.Empty;
                    SearchCommitFilterSuggestion.Clear();
                    IsSearchCommitSuggestionOpen = false;
                    _revisionFiles.Clear();

                    if (value)
                    {
                        SelectedViewIndex = 0;
                        UpdateCurrentRevisionFilesForSearchSuggestion();
                    }
                }
            }
        }

        public bool IsSearchLoadingVisible
        {
            get => _isSearchLoadingVisible;
            private set => SetProperty(ref _isSearchLoadingVisible, value);
        }

        public bool OnlySearchCommitsInCurrentBranch
        {
            get => _onlySearchCommitsInCurrentBranch;
            set
            {
                if (SetProperty(ref _onlySearchCommitsInCurrentBranch, value) && !string.IsNullOrEmpty(_searchCommitFilter))
                    StartSearchCommits();
            }
        }

        public int SearchCommitFilterType
        {
            get => _searchCommitFilterType;
            set
            {
                if (SetProperty(ref _searchCommitFilterType, value))
                {
                    UpdateCurrentRevisionFilesForSearchSuggestion();

                    if (!string.IsNullOrEmpty(_searchCommitFilter))
                        StartSearchCommits();
                }
            }
        }

        public string SearchCommitFilter
        {
            get => _searchCommitFilter;
            set
            {
                if (SetProperty(ref _searchCommitFilter, value) &&
                    _searchCommitFilterType == 3 &&
                    !string.IsNullOrEmpty(value) &&
                    value.Length >= 2 &&
                    _revisionFiles.Count > 0)
                {
                    var suggestion = new List<string>();
                    foreach (var file in _revisionFiles)
                    {
                        if (file.Contains(value, StringComparison.OrdinalIgnoreCase) && file.Length != value.Length)
                        {
                            suggestion.Add(file);
                            if (suggestion.Count > 100)
                                break;
                        }
                    }

                    SearchCommitFilterSuggestion.Clear();
                    SearchCommitFilterSuggestion.AddRange(suggestion);
                    IsSearchCommitSuggestionOpen = SearchCommitFilterSuggestion.Count > 0;
                }
                else if (SearchCommitFilterSuggestion.Count > 0)
                {
                    SearchCommitFilterSuggestion.Clear();
                    IsSearchCommitSuggestionOpen = false;
                }
            }
        }

        public bool IsSearchCommitSuggestionOpen
        {
            get => _isSearchCommitSuggestionOpen;
            set => SetProperty(ref _isSearchCommitSuggestionOpen, value);
        }

        public AvaloniaList<string> SearchCommitFilterSuggestion
        {
            get;
            private set;
        } = new AvaloniaList<string>();

        public List<Models.Commit> SearchedCommits
        {
            get => _searchedCommits;
            set => SetProperty(ref _searchedCommits, value);
        }

        public bool IsLocalBranchGroupExpanded
        {
            get => _settings.IsLocalBranchesExpandedInSideBar;
            set
            {
                if (value != _settings.IsLocalBranchesExpandedInSideBar)
                {
                    _settings.IsLocalBranchesExpandedInSideBar = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsRemoteGroupExpanded
        {
            get => _settings.IsRemotesExpandedInSideBar;
            set
            {
                if (value != _settings.IsRemotesExpandedInSideBar)
                {
                    _settings.IsRemotesExpandedInSideBar = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsTagGroupExpanded
        {
            get => _settings.IsTagsExpandedInSideBar;
            set
            {
                if (value != _settings.IsTagsExpandedInSideBar)
                {
                    _settings.IsTagsExpandedInSideBar = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSubmoduleGroupExpanded
        {
            get => _settings.IsSubmodulesExpandedInSideBar;
            set
            {
                if (value != _settings.IsSubmodulesExpandedInSideBar)
                {
                    _settings.IsSubmodulesExpandedInSideBar = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsWorktreeGroupExpanded
        {
            get => _settings.IsWorktreeExpandedInSideBar;
            set
            {
                if (value != _settings.IsWorktreeExpandedInSideBar)
                {
                    _settings.IsWorktreeExpandedInSideBar = value;
                    OnPropertyChanged();
                }
            }
        }

        public InProgressContext InProgressContext
        {
            get => _workingCopy?.InProgressContext;
        }

        public Models.Commit SearchResultSelectedCommit
        {
            get => _searchResultSelectedCommit;
            set
            {
                if (SetProperty(ref _searchResultSelectedCommit, value) && value != null)
                    NavigateToCommit(value.SHA);
            }
        }

        public bool IsAutoFetching
        {
            get => _isAutoFetching;
            private set => SetProperty(ref _isAutoFetching, value);
        }

        public void Open()
        {
            var settingsFile = Path.Combine(_gitDir, "sourcegit.settings");
            if (File.Exists(settingsFile))
            {
                try
                {
                    _settings = JsonSerializer.Deserialize(File.ReadAllText(settingsFile), JsonCodeGen.Default.RepositorySettings);
                }
                catch
                {
                    _settings = new Models.RepositorySettings();
                }
            }
            else
            {
                _settings = new Models.RepositorySettings();
            }

            try
            {
                _watcher = new Models.Watcher(this);
            }
            catch (Exception ex)
            {
                App.RaiseException(string.Empty, $"Failed to start watcher for repository: '{_fullpath}'. You may need to press 'F5' to refresh repository manually!\n\nReason: {ex.Message}");
            }

            if (_settings.HistoriesFilters.Count > 0)
                _historiesFilterMode = _settings.HistoriesFilters[0].Mode;
            else
                _historiesFilterMode = Models.FilterMode.None;

            _histories = new Histories(this);
            _workingCopy = new WorkingCopy(this);
            _stashesPage = new StashesPage(this);
            _selectedView = _histories;
            _selectedViewIndex = 0;

            _autoFetchTimer = new Timer(AutoFetchImpl, null, 5000, 5000);
            RefreshAll();
        }

        public void Close()
        {
            SelectedView = null; // Do NOT modify. Used to remove exists widgets for GC.Collect

            var settingsSerialized = JsonSerializer.Serialize(_settings, JsonCodeGen.Default.RepositorySettings);
            try
            {
                File.WriteAllText(Path.Combine(_gitDir, "sourcegit.settings"), settingsSerialized);
            }
            catch (DirectoryNotFoundException)
            {
                // Ignore
            }
            _autoFetchTimer.Dispose();
            _autoFetchTimer = null;

            _settings = null;
            _historiesFilterMode = Models.FilterMode.None;

            _watcher?.Dispose();
            _histories.Cleanup();
            _workingCopy.Cleanup();
            _stashesPage.Cleanup();

            _watcher = null;
            _histories = null;
            _workingCopy = null;
            _stashesPage = null;

            _localChangesCount = 0;
            _stashesCount = 0;

            _remotes.Clear();
            _branches.Clear();
            _localBranchTrees.Clear();
            _remoteBranchTrees.Clear();
            _tags.Clear();
            _visibleTags.Clear();
            _submodules.Clear();
            _visibleSubmodules.Clear();
            _searchedCommits.Clear();

            _revisionFiles.Clear();
            SearchCommitFilterSuggestion.Clear();
        }

        public bool CanCreatePopup()
        {
            var page = GetOwnerPage();
            if (page == null)
                return false;

            return !_isAutoFetching && page.CanCreatePopup();
        }

        public void ShowPopup(Popup popup)
        {
            var page = GetOwnerPage();
            if (page != null)
                page.Popup = popup;
        }

        public void ShowAndStartPopup(Popup popup)
        {
            GetOwnerPage()?.StartPopup(popup);
        }

        public void RefreshAll()
        {
            Task.Run(() =>
            {
                var allowedSignersFile = new Commands.Config(_fullpath).Get("gpg.ssh.allowedSignersFile");
                _hasAllowedSignersFile = !string.IsNullOrEmpty(allowedSignersFile);
            });

            Task.Run(RefreshBranches);
            Task.Run(RefreshTags);
            Task.Run(RefreshCommits);
            Task.Run(RefreshSubmodules);
            Task.Run(RefreshWorktrees);
            Task.Run(RefreshWorkingCopyChanges);
            Task.Run(RefreshStashes);
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
                item.Click += (_, e) =>
                {
                    dupTool.Open(_fullpath);
                    e.Handled = true;
                };

                menu.Items.Add(item);
            }

            return menu;
        }

        public void Fetch(bool autoStart)
        {
            if (!CanCreatePopup())
                return;

            if (_remotes.Count == 0)
            {
                App.RaiseException(_fullpath, "No remotes added to this repository!!!");
                return;
            }

            if (autoStart)
                ShowAndStartPopup(new Fetch(this));
            else
                ShowPopup(new Fetch(this));
        }

        public void Pull(bool autoStart)
        {
            if (!CanCreatePopup())
                return;

            if (_remotes.Count == 0)
            {
                App.RaiseException(_fullpath, "No remotes added to this repository!!!");
                return;
            }

            var pull = new Pull(this, null);
            if (autoStart && pull.SelectedBranch != null)
                ShowAndStartPopup(pull);
            else
                ShowPopup(pull);
        }

        public void Push(bool autoStart)
        {
            if (!CanCreatePopup())
                return;

            if (_remotes.Count == 0)
            {
                App.RaiseException(_fullpath, "No remotes added to this repository!!!");
                return;
            }

            if (_currentBranch == null)
            {
                App.RaiseException(_fullpath, "Can NOT found current branch!!!");
                return;
            }

            if (autoStart)
                ShowAndStartPopup(new Push(this, null));
            else
                ShowPopup(new Push(this, null));
        }

        public void ApplyPatch()
        {
            if (!CanCreatePopup())
                return;
            ShowPopup(new Apply(this));
        }

        public void Cleanup()
        {
            if (!CanCreatePopup())
                return;
            ShowAndStartPopup(new Cleanup(this));
        }

        public void ClearFilter()
        {
            Filter = string.Empty;
        }

        public void ClearSearchCommitFilter()
        {
            SearchCommitFilter = string.Empty;
        }

        public void StartSearchCommits()
        {
            if (_histories == null)
                return;

            IsSearchLoadingVisible = true;
            SearchResultSelectedCommit = null;
            IsSearchCommitSuggestionOpen = false;
            SearchCommitFilterSuggestion.Clear();

            Task.Run(() =>
            {
                var visible = new List<Models.Commit>();

                switch (_searchCommitFilterType)
                {
                    case 0:
                        var commit = new Commands.QuerySingleCommit(_fullpath, _searchCommitFilter).Result();
                        if (commit != null)
                            visible.Add(commit);
                        break;
                    case 1:
                        visible = new Commands.QueryCommits(_fullpath, _searchCommitFilter, Models.CommitSearchMethod.ByUser, _onlySearchCommitsInCurrentBranch).Result();
                        break;
                    case 2:
                        visible = new Commands.QueryCommits(_fullpath, _searchCommitFilter, Models.CommitSearchMethod.ByMessage, _onlySearchCommitsInCurrentBranch).Result();
                        break;
                    case 3:
                        visible = new Commands.QueryCommits(_fullpath, _searchCommitFilter, Models.CommitSearchMethod.ByFile, _onlySearchCommitsInCurrentBranch).Result();
                        break;
                }

                Dispatcher.UIThread.Invoke(() =>
                {
                    SearchedCommits = visible;
                    IsSearchLoadingVisible = false;
                });
            });
        }

        public void SetWatcherEnabled(bool enabled)
        {
            _watcher?.SetEnabled(enabled);
        }

        public void MarkBranchesDirtyManually()
        {
            if (_watcher == null)
            {
                Task.Run(RefreshBranches);
                Task.Run(RefreshCommits);
                Task.Run(RefreshWorkingCopyChanges);
                Task.Run(RefreshWorktrees);
            }
            else
            {
                _watcher.MarkBranchDirtyManually();
            }
        }

        public void MarkWorkingCopyDirtyManually()
        {
            if (_watcher == null)
                Task.Run(RefreshWorkingCopyChanges);
            else
                _watcher.MarkWorkingCopyDirtyManually();
        }

        public void MarkFetched()
        {
            _lastFetchTime = DateTime.Now;
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
            if (_currentBranch != null)
                NavigateToCommit(_currentBranch.Head);
        }

        public void ClearHistoriesFilter()
        {
            _settings.HistoriesFilters.Clear();
            HistoriesFilterMode = Models.FilterMode.None;

            ResetBranchTreeFilterMode(LocalBranchTrees);
            ResetBranchTreeFilterMode(RemoteBranchTrees);
            ResetTagFilterMode();
            Task.Run(RefreshCommits);
        }

        public void UpdateBranchNodeIsExpanded(BranchTreeNode node)
        {
            if (_settings == null || !string.IsNullOrWhiteSpace(_filter))
                return;

            if (node.IsExpanded)
            {
                if (!_settings.ExpandedBranchNodesInSideBar.Contains(node.Path))
                    _settings.ExpandedBranchNodesInSideBar.Add(node.Path);
            }
            else
            {
                _settings.ExpandedBranchNodesInSideBar.Remove(node.Path);
            }
        }

        public void SetTagFilterMode(Models.Tag tag, Models.FilterMode mode)
        {
            var changed = _settings.UpdateHistoriesFilter(tag.Name, Models.FilterType.Tag, mode);
            if (changed)
                RefreshHistoriesFilters(true);
        }

        public void SetBranchFilterMode(Models.Branch branch, Models.FilterMode mode, bool clearExists, bool refresh)
        {
            var node = FindBranchNode(branch.IsLocal ? _localBranchTrees : _remoteBranchTrees, branch.FullName);
            if (node != null)
                SetBranchFilterMode(node, mode, clearExists, refresh);
        }

        public void SetBranchFilterMode(BranchTreeNode node, Models.FilterMode mode, bool clearExists, bool refresh)
        {
            var isLocal = node.Path.StartsWith("refs/heads/", StringComparison.Ordinal);
            var tree = isLocal ? _localBranchTrees : _remoteBranchTrees;

            if (clearExists)
            {
                _settings.HistoriesFilters.Clear();
                HistoriesFilterMode = Models.FilterMode.None;
            }

            if (node.Backend is Models.Branch branch)
            {
                var type = isLocal ? Models.FilterType.LocalBranch : Models.FilterType.RemoteBranch;
                var changed = _settings.UpdateHistoriesFilter(node.Path, type, mode);
                if (!changed)
                    return;

                if (isLocal && !string.IsNullOrEmpty(branch.Upstream))
                    _settings.UpdateHistoriesFilter(branch.Upstream, Models.FilterType.RemoteBranch, mode);
            }
            else
            {
                var type = isLocal ? Models.FilterType.LocalBranchFolder : Models.FilterType.RemoteBranchFolder;
                var changed = _settings.UpdateHistoriesFilter(node.Path, type, mode);
                if (!changed)
                    return;

                _settings.RemoveChildrenBranchFilters(node.Path);
            }

            var parentType = isLocal ? Models.FilterType.LocalBranchFolder : Models.FilterType.RemoteBranchFolder;
            var cur = node;
            do
            {
                var lastSepIdx = cur.Path.LastIndexOf('/');
                if (lastSepIdx <= 0)
                    break;

                var parentPath = cur.Path.Substring(0, lastSepIdx);
                var parent = FindBranchNode(tree, parentPath);
                if (parent == null)
                    break;

                _settings.UpdateHistoriesFilter(parent.Path, parentType, Models.FilterMode.None);
                cur = parent;
            } while (true);

            RefreshHistoriesFilters(refresh);
        }

        public void StashAll(bool autoStart)
        {
            _workingCopy?.StashAll(autoStart);
        }

        public void SkipMerge()
        {
            _workingCopy?.SkipMerge();
        }

        public void AbortMerge()
        {
            _workingCopy?.AbortMerge();
        }

        public void RefreshBranches()
        {
            var branches = new Commands.QueryBranches(_fullpath).Result();
            var remotes = new Commands.QueryRemotes(_fullpath).Result();
            var builder = BuildBranchTree(branches, remotes);

            Dispatcher.UIThread.Invoke(() =>
            {
                lock (_lockRemotes)
                    Remotes = remotes;

                Branches = branches;
                CurrentBranch = branches.Find(x => x.IsCurrent);
                LocalBranchTrees = builder.Locals;
                RemoteBranchTrees = builder.Remotes;

                if (_workingCopy != null)
                    _workingCopy.CanCommitWithPush = _currentBranch != null && !string.IsNullOrEmpty(_currentBranch.Upstream);
            });
        }

        public void RefreshWorktrees()
        {
            var worktrees = new Commands.Worktree(_fullpath).List();
            var cleaned = new List<Models.Worktree>();

            foreach (var worktree in worktrees)
            {
                if (worktree.IsBare || worktree.FullPath.Equals(_fullpath))
                    continue;

                cleaned.Add(worktree);
            }

            Dispatcher.UIThread.Invoke(() =>
            {
                Worktrees = cleaned;
            });
        }

        public void RefreshTags()
        {
            var tags = new Commands.QueryTags(_fullpath).Result();
            Dispatcher.UIThread.Invoke(() =>
            {
                Tags = tags;
                VisibleTags = BuildVisibleTags();
            });
        }

        public void RefreshCommits()
        {
            Dispatcher.UIThread.Invoke(() => _histories.IsLoading = true);

            var builder = new StringBuilder();
            builder.Append($"-{Preference.Instance.MaxHistoryCommits} ");

            if (_settings.EnableTopoOrderInHistories)
                builder.Append("--topo-order ");
            else
                builder.Append("--date-order ");

            if (_settings.EnableReflog)
                builder.Append("--reflog ");
            if (_settings.EnableFirstParentInHistories)
                builder.Append("--first-parent ");

            var filters = _settings.BuildHistoriesFilter();
            if (string.IsNullOrEmpty(filters))
                builder.Append("--branches --remotes --tags");
            else
                builder.Append(filters);

            var commits = new Commands.QueryCommits(_fullpath, builder.ToString()).Result();
            var graph = Models.CommitGraph.Parse(commits, _settings.EnableFirstParentInHistories);

            Dispatcher.UIThread.Invoke(() =>
            {
                if (_histories != null)
                {
                    _histories.IsLoading = false;
                    _histories.Commits = commits;
                    _histories.Graph = graph;
                }
            });
        }

        public void RefreshSubmodules()
        {
            var submodules = new Commands.QuerySubmodules(_fullpath).Result();
            _watcher?.SetSubmodules(submodules);

            Dispatcher.UIThread.Invoke(() =>
            {
                Submodules = submodules;
                VisibleSubmodules = BuildVisibleSubmodules();
            });
        }

        public void RefreshWorkingCopyChanges()
        {
            var changes = new Commands.QueryLocalChanges(_fullpath, _settings.IncludeUntrackedInLocalChanges).Result();
            if (_workingCopy == null)
                return;

            _workingCopy.SetData(changes);

            Dispatcher.UIThread.Invoke(() =>
            {
                LocalChangesCount = changes.Count;
                OnPropertyChanged(nameof(InProgressContext));
            });
        }

        public void RefreshStashes()
        {
            var stashes = new Commands.QueryStashes(_fullpath).Result();
            Dispatcher.UIThread.Invoke(() =>
            {
                if (_stashesPage != null)
                    _stashesPage.Stashes = stashes;

                StashesCount = stashes.Count;
            });
        }

        public void CreateNewBranch()
        {
            if (_currentBranch == null)
            {
                App.RaiseException(_fullpath, "Git do not hold any branch until you do first commit.");
                return;
            }

            if (CanCreatePopup())
                ShowPopup(new CreateBranch(this, _currentBranch));
        }

        public void CheckoutBranch(Models.Branch branch)
        {
            if (branch.IsLocal)
            {
                var worktree = _worktrees.Find(x => x.Branch == branch.FullName);
                if (worktree != null)
                {
                    OpenWorktree(worktree);
                    return;
                }
            }

            if (!CanCreatePopup())
                return;

            if (branch.IsLocal)
            {
                if (_localChangesCount > 0)
                    ShowPopup(new Checkout(this, branch.Name));
                else
                    ShowAndStartPopup(new Checkout(this, branch.Name));
            }
            else
            {
                foreach (var b in _branches)
                {
                    if (b.IsLocal && b.Upstream == branch.FullName)
                    {
                        if (!b.IsCurrent)
                            CheckoutBranch(b);

                        return;
                    }
                }

                ShowPopup(new CreateBranch(this, branch));
            }
        }

        public void DeleteMultipleBranches(List<Models.Branch> branches, bool isLocal)
        {
            if (CanCreatePopup())
                ShowPopup(new DeleteMultipleBranches(this, branches, isLocal));
        }

        public void MergeMultipleBranches(List<Models.Branch> branches)
        {
            if (CanCreatePopup())
                ShowPopup(new MergeMultiple(this, branches));
        }

        public void CreateNewTag()
        {
            if (_currentBranch == null)
            {
                App.RaiseException(_fullpath, "Git do not hold any branch until you do first commit.");
                return;
            }

            if (CanCreatePopup())
                ShowPopup(new CreateTag(this, _currentBranch));
        }

        public void AddRemote()
        {
            if (CanCreatePopup())
                ShowPopup(new AddRemote(this));
        }

        public void AddSubmodule()
        {
            if (CanCreatePopup())
                ShowPopup(new AddSubmodule(this));
        }

        public void UpdateSubmodules()
        {
            if (CanCreatePopup())
                ShowPopup(new UpdateSubmodules(this));
        }

        public void OpenSubmodule(string submodule)
        {
            var root = Path.GetFullPath(Path.Combine(_fullpath, submodule));
            var normalizedPath = root.Replace("\\", "/");

            var node = Preference.Instance.FindNode(normalizedPath);
            if (node == null)
            {
                node = new RepositoryNode()
                {
                    Id = normalizedPath,
                    Name = Path.GetFileName(normalizedPath),
                    Bookmark = 0,
                    IsRepository = true,
                };
            }

            App.GetLauncer()?.OpenRepositoryInTab(node, null);
        }

        public void AddWorktree()
        {
            if (CanCreatePopup())
                ShowPopup(new AddWorktree(this));
        }

        public void PruneWorktrees()
        {
            if (CanCreatePopup())
                ShowAndStartPopup(new PruneWorktrees(this));
        }

        public void OpenWorktree(Models.Worktree worktree)
        {
            var node = Preference.Instance.FindNode(worktree.FullPath);
            if (node == null)
            {
                node = new RepositoryNode()
                {
                    Id = worktree.FullPath,
                    Name = Path.GetFileName(worktree.FullPath),
                    Bookmark = 0,
                    IsRepository = true,
                };
            }

            App.GetLauncer()?.OpenRepositoryInTab(node, null);
        }

        public ContextMenu CreateContextMenuForGitFlow()
        {
            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

            var isGitFlowEnabled = Commands.GitFlow.IsEnabled(_fullpath, _branches);
            if (isGitFlowEnabled)
            {
                var startFeature = new MenuItem();
                startFeature.Header = App.Text("GitFlow.StartFeature");
                startFeature.Icon = App.CreateMenuIcon("Icons.GitFlow.Feature");
                startFeature.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new GitFlowStart(this, "feature"));
                    e.Handled = true;
                };

                var startRelease = new MenuItem();
                startRelease.Header = App.Text("GitFlow.StartRelease");
                startRelease.Icon = App.CreateMenuIcon("Icons.GitFlow.Release");
                startRelease.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new GitFlowStart(this, "release"));
                    e.Handled = true;
                };

                var startHotfix = new MenuItem();
                startHotfix.Header = App.Text("GitFlow.StartHotfix");
                startHotfix.Icon = App.CreateMenuIcon("Icons.GitFlow.Hotfix");
                startHotfix.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new GitFlowStart(this, "hotfix"));
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
                init.Icon = App.CreateMenuIcon("Icons.Init");
                init.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new InitGitFlow(this));
                    e.Handled = true;
                };
                menu.Items.Add(init);
            }
            return menu;
        }

        public ContextMenu CreateContextMenuForGitLFS()
        {
            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

            var lfs = new Commands.LFS(_fullpath);
            if (lfs.IsEnabled())
            {
                var addPattern = new MenuItem();
                addPattern.Header = App.Text("GitLFS.AddTrackPattern");
                addPattern.Icon = App.CreateMenuIcon("Icons.File.Add");
                addPattern.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new LFSTrackCustomPattern(this));

                    e.Handled = true;
                };
                menu.Items.Add(addPattern);
                menu.Items.Add(new MenuItem() { Header = "-" });

                var fetch = new MenuItem();
                fetch.Header = App.Text("GitLFS.Fetch");
                fetch.Icon = App.CreateMenuIcon("Icons.Fetch");
                fetch.IsEnabled = _remotes.Count > 0;
                fetch.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                    {
                        if (_remotes.Count == 1)
                            ShowAndStartPopup(new LFSFetch(this));
                        else
                            ShowPopup(new LFSFetch(this));
                    }

                    e.Handled = true;
                };
                menu.Items.Add(fetch);

                var pull = new MenuItem();
                pull.Header = App.Text("GitLFS.Pull");
                pull.Icon = App.CreateMenuIcon("Icons.Pull");
                pull.IsEnabled = _remotes.Count > 0;
                pull.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                    {
                        if (_remotes.Count == 1)
                            ShowAndStartPopup(new LFSPull(this));
                        else
                            ShowPopup(new LFSPull(this));
                    }

                    e.Handled = true;
                };
                menu.Items.Add(pull);

                var push = new MenuItem();
                push.Header = App.Text("GitLFS.Push");
                push.Icon = App.CreateMenuIcon("Icons.Push");
                push.IsEnabled = _remotes.Count > 0;
                push.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                    {
                        if (_remotes.Count == 1)
                            ShowAndStartPopup(new LFSPush(this));
                        else
                            ShowPopup(new LFSPush(this));
                    }

                    e.Handled = true;
                };
                menu.Items.Add(push);

                var prune = new MenuItem();
                prune.Header = App.Text("GitLFS.Prune");
                prune.Icon = App.CreateMenuIcon("Icons.Clean");
                prune.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowAndStartPopup(new LFSPrune(this));

                    e.Handled = true;
                };
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(prune);

                var locks = new MenuItem();
                locks.Header = App.Text("GitLFS.Locks");
                locks.Icon = App.CreateMenuIcon("Icons.Lock");
                locks.IsEnabled = _remotes.Count > 0;
                if (_remotes.Count == 1)
                {
                    locks.Click += (_, e) =>
                    {
                        var dialog = new Views.LFSLocks() { DataContext = new LFSLocks(_fullpath, _remotes[0].Name) };
                        App.OpenDialog(dialog);
                        e.Handled = true;
                    };
                }
                else
                {
                    foreach (var remote in _remotes)
                    {
                        var remoteName = remote.Name;
                        var lockRemote = new MenuItem();
                        lockRemote.Header = remoteName;
                        lockRemote.Click += (_, e) =>
                        {
                            var dialog = new Views.LFSLocks() { DataContext = new LFSLocks(_fullpath, remoteName) };
                            App.OpenDialog(dialog);
                            e.Handled = true;
                        };
                        locks.Items.Add(lockRemote);
                    }
                }

                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(locks);
            }
            else
            {
                var install = new MenuItem();
                install.Header = App.Text("GitLFS.Install");
                install.Icon = App.CreateMenuIcon("Icons.Init");
                install.Click += (_, e) =>
                {
                    var succ = new Commands.LFS(_fullpath).Install();
                    if (succ)
                        App.SendNotification(_fullpath, $"LFS enabled successfully!");

                    e.Handled = true;
                };
                menu.Items.Add(install);
            }

            return menu;
        }

        public ContextMenu CreateContextMenuForCustomAction()
        {
            var actions = new List<Models.CustomAction>();
            foreach (var action in _settings.CustomActions)
            {
                if (action.Scope == Models.CustomActionScope.Repository)
                    actions.Add(action);
            }

            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

            if (actions.Count > 0)
            {
                foreach (var action in actions)
                {
                    var dup = action;
                    var item = new MenuItem();
                    item.Icon = App.CreateMenuIcon("Icons.Action");
                    item.Header = dup.Name;
                    item.Click += (_, e) =>
                    {
                        if (CanCreatePopup())
                            ShowAndStartPopup(new ExecuteCustomAction(this, dup, null));

                        e.Handled = true;
                    };

                    menu.Items.Add(item);
                }
            }
            else
            {
                menu.Items.Add(new MenuItem() { Header = App.Text("Repository.CustomActions.Empty") });
            }

            return menu;
        }

        public ContextMenu CreateContextMenuForLocalBranch(Models.Branch branch)
        {
            var menu = new ContextMenu();

            var push = new MenuItem();
            push.Header = new Views.NameHighlightedTextBlock("BranchCM.Push", branch.Name);
            push.Icon = App.CreateMenuIcon("Icons.Push");
            push.IsEnabled = _remotes.Count > 0;
            push.Click += (_, e) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new Push(this, branch));
                e.Handled = true;
            };

            if (branch.IsCurrent)
            {
                var discard = new MenuItem();
                discard.Header = App.Text("BranchCM.DiscardAll");
                discard.Icon = App.CreateMenuIcon("Icons.Undo");
                discard.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new Discard(this));
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
                    fastForward.IsEnabled = branch.TrackStatus.Ahead.Count == 0;
                    fastForward.Click += (_, e) =>
                    {
                        var b = _branches.Find(x => x.FriendlyName == upstream);
                        if (b == null)
                            return;

                        if (CanCreatePopup())
                            ShowAndStartPopup(new Merge(this, b, branch.Name));

                        e.Handled = true;
                    };

                    var pull = new MenuItem();
                    pull.Header = new Views.NameHighlightedTextBlock("BranchCM.Pull", upstream);
                    pull.Icon = App.CreateMenuIcon("Icons.Pull");
                    pull.Click += (_, e) =>
                    {
                        if (CanCreatePopup())
                            ShowPopup(new Pull(this, null));
                        e.Handled = true;
                    };

                    menu.Items.Add(fastForward);
                    menu.Items.Add(pull);
                }

                menu.Items.Add(push);

                var compareWithBranch = CreateMenuItemToCompareBranches(branch);
                if (compareWithBranch != null)
                {
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(compareWithBranch);
                }
            }
            else
            {
                var checkout = new MenuItem();
                checkout.Header = new Views.NameHighlightedTextBlock("BranchCM.Checkout", branch.Name);
                checkout.Icon = App.CreateMenuIcon("Icons.Check");
                checkout.Click += (_, e) =>
                {
                    CheckoutBranch(branch);
                    e.Handled = true;
                };
                menu.Items.Add(checkout);
                menu.Items.Add(new MenuItem() { Header = "-" });

                var worktree = _worktrees.Find(x => x.Branch == branch.FullName);
                var upstream = _branches.Find(x => x.FullName == branch.Upstream);
                if (upstream != null && worktree == null)
                {
                    var fastForward = new MenuItem();
                    fastForward.Header = new Views.NameHighlightedTextBlock("BranchCM.FastForward", upstream.FriendlyName);
                    fastForward.Icon = App.CreateMenuIcon("Icons.FastForward");
                    fastForward.IsEnabled = branch.TrackStatus.Ahead.Count == 0;
                    fastForward.Click += (_, e) =>
                    {
                        if (CanCreatePopup())
                            ShowAndStartPopup(new FastForwardWithoutCheckout(this, branch, upstream));
                        e.Handled = true;
                    };

                    var fetchInto = new MenuItem();
                    fetchInto.Header = new Views.NameHighlightedTextBlock("BranchCM.FetchInto", upstream.FriendlyName, branch.Name);
                    fetchInto.Icon = App.CreateMenuIcon("Icons.Fetch");
                    fetchInto.IsEnabled = branch.TrackStatus.Ahead.Count == 0;
                    fetchInto.Click += (_, e) =>
                    {
                        if (CanCreatePopup())
                            ShowAndStartPopup(new FetchInto(this, branch, upstream));
                        e.Handled = true;
                    };

                    menu.Items.Add(fastForward);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(fetchInto);
                }

                menu.Items.Add(push);

                var merge = new MenuItem();
                merge.Header = new Views.NameHighlightedTextBlock("BranchCM.Merge", branch.Name, _currentBranch.Name);
                merge.Icon = App.CreateMenuIcon("Icons.Merge");
                merge.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new Merge(this, branch, _currentBranch.Name));
                    e.Handled = true;
                };

                var rebase = new MenuItem();
                rebase.Header = new Views.NameHighlightedTextBlock("BranchCM.Rebase", _currentBranch.Name, branch.Name);
                rebase.Icon = App.CreateMenuIcon("Icons.Rebase");
                rebase.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new Rebase(this, _currentBranch, branch));
                    e.Handled = true;
                };

                menu.Items.Add(merge);
                menu.Items.Add(rebase);

                if (_localChangesCount > 0)
                {
                    var compareWithWorktree = new MenuItem();
                    compareWithWorktree.Header = App.Text("BranchCM.CompareWithWorktree");
                    compareWithWorktree.Icon = App.CreateMenuIcon("Icons.Compare");
                    compareWithWorktree.Click += (_, _) =>
                    {
                        SearchResultSelectedCommit = null;

                        if (_histories != null)
                        {
                            var target = new Commands.QuerySingleCommit(_fullpath, branch.Head).Result();
                            _histories.AutoSelectedCommit = null;
                            _histories.DetailContext = new RevisionCompare(_fullpath, target, null);
                        }
                    };
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(compareWithWorktree);
                }

                var compareWithBranch = CreateMenuItemToCompareBranches(branch);
                if (compareWithBranch != null)
                {
                    if (_localChangesCount == 0)
                        menu.Items.Add(new MenuItem() { Header = "-" });

                    menu.Items.Add(compareWithBranch);
                }
            }

            var detect = Commands.GitFlow.DetectType(_fullpath, _branches, branch.Name);
            if (detect.IsGitFlowBranch)
            {
                var finish = new MenuItem();
                finish.Header = new Views.NameHighlightedTextBlock("BranchCM.Finish", branch.Name);
                finish.Icon = App.CreateMenuIcon("Icons.GitFlow");
                finish.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new GitFlowFinish(this, branch, detect.Type, detect.Prefix));
                    e.Handled = true;
                };
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(finish);
            }

            var rename = new MenuItem();
            rename.Header = new Views.NameHighlightedTextBlock("BranchCM.Rename", branch.Name);
            rename.Icon = App.CreateMenuIcon("Icons.Rename");
            rename.Click += (_, e) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new RenameBranch(this, branch));
                e.Handled = true;
            };

            var delete = new MenuItem();
            delete.Header = new Views.NameHighlightedTextBlock("BranchCM.Delete", branch.Name);
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
            delete.IsEnabled = !branch.IsCurrent;
            delete.Click += (_, e) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new DeleteBranch(this, branch));
                e.Handled = true;
            };

            var createBranch = new MenuItem();
            createBranch.Icon = App.CreateMenuIcon("Icons.Branch.Add");
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Click += (_, e) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new CreateBranch(this, branch));
                e.Handled = true;
            };

            var createTag = new MenuItem();
            createTag.Icon = App.CreateMenuIcon("Icons.Tag.Add");
            createTag.Header = App.Text("CreateTag");
            createTag.Click += (_, e) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new CreateTag(this, branch));
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
            foreach (var b in _branches)
            {
                if (!b.IsLocal)
                    remoteBranches.Add(b);
            }

            if (remoteBranches.Count > 0)
            {
                var tracking = new MenuItem();
                tracking.Header = App.Text("BranchCM.Tracking");
                tracking.Icon = App.CreateMenuIcon("Icons.Track");
                tracking.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new SetUpstream(this, branch, remoteBranches));
                    e.Handled = true;
                };
                menu.Items.Add(tracking);
            }

            var archive = new MenuItem();
            archive.Icon = App.CreateMenuIcon("Icons.Archive");
            archive.Header = App.Text("Archive");
            archive.Click += (_, e) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new Archive(this, branch));
                e.Handled = true;
            };
            menu.Items.Add(archive);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (_, e) =>
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

            if (remote.TryGetVisitURL(out string visitURL))
            {
                var visit = new MenuItem();
                visit.Header = App.Text("RemoteCM.OpenInBrowser");
                visit.Icon = App.CreateMenuIcon("Icons.OpenWith");
                visit.Click += (_, e) =>
                {
                    Native.OS.OpenBrowser(visitURL);
                    e.Handled = true;
                };

                menu.Items.Add(visit);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var fetch = new MenuItem();
            fetch.Header = App.Text("RemoteCM.Fetch");
            fetch.Icon = App.CreateMenuIcon("Icons.Fetch");
            fetch.Click += (_, e) =>
            {
                if (CanCreatePopup())
                    ShowAndStartPopup(new Fetch(this, remote));
                e.Handled = true;
            };

            var prune = new MenuItem();
            prune.Header = App.Text("RemoteCM.Prune");
            prune.Icon = App.CreateMenuIcon("Icons.Clean");
            prune.Click += (_, e) =>
            {
                if (CanCreatePopup())
                    ShowAndStartPopup(new PruneRemote(this, remote));
                e.Handled = true;
            };

            var edit = new MenuItem();
            edit.Header = App.Text("RemoteCM.Edit");
            edit.Icon = App.CreateMenuIcon("Icons.Edit");
            edit.Click += (_, e) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new EditRemote(this, remote));
                e.Handled = true;
            };

            var delete = new MenuItem();
            delete.Header = App.Text("RemoteCM.Delete");
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new DeleteRemote(this, remote));
                e.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("RemoteCM.CopyURL");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (_, e) =>
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
            var name = branch.FriendlyName;

            var checkout = new MenuItem();
            checkout.Header = new Views.NameHighlightedTextBlock("BranchCM.Checkout", name);
            checkout.Icon = App.CreateMenuIcon("Icons.Check");
            checkout.Click += (_, e) =>
            {
                CheckoutBranch(branch);
                e.Handled = true;
            };
            menu.Items.Add(checkout);
            menu.Items.Add(new MenuItem() { Header = "-" });

            if (_currentBranch != null)
            {
                var pull = new MenuItem();
                pull.Header = new Views.NameHighlightedTextBlock("BranchCM.PullInto", name, _currentBranch.Name);
                pull.Icon = App.CreateMenuIcon("Icons.Pull");
                pull.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new Pull(this, branch));
                    e.Handled = true;
                };

                var merge = new MenuItem();
                merge.Header = new Views.NameHighlightedTextBlock("BranchCM.Merge", name, _currentBranch.Name);
                merge.Icon = App.CreateMenuIcon("Icons.Merge");
                merge.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new Merge(this, branch, _currentBranch.Name));
                    e.Handled = true;
                };

                var rebase = new MenuItem();
                rebase.Header = new Views.NameHighlightedTextBlock("BranchCM.Rebase", _currentBranch.Name, name);
                rebase.Icon = App.CreateMenuIcon("Icons.Rebase");
                rebase.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new Rebase(this, _currentBranch, branch));
                    e.Handled = true;
                };

                menu.Items.Add(pull);
                menu.Items.Add(merge);
                menu.Items.Add(rebase);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var hasCompare = false;
            if (_localChangesCount > 0)
            {
                var compareWithWorktree = new MenuItem();
                compareWithWorktree.Header = App.Text("BranchCM.CompareWithWorktree");
                compareWithWorktree.Icon = App.CreateMenuIcon("Icons.Compare");
                compareWithWorktree.Click += (_, _) =>
                {
                    SearchResultSelectedCommit = null;

                    if (_histories != null)
                    {
                        var target = new Commands.QuerySingleCommit(_fullpath, branch.Head).Result();
                        _histories.AutoSelectedCommit = null;
                        _histories.DetailContext = new RevisionCompare(_fullpath, target, null);
                    }
                };
                menu.Items.Add(compareWithWorktree);
                hasCompare = true;
            }

            var compareWithBranch = CreateMenuItemToCompareBranches(branch);
            if (compareWithBranch != null)
            {
                menu.Items.Add(compareWithBranch);
                hasCompare = true;
            }

            if (hasCompare)
                menu.Items.Add(new MenuItem() { Header = "-" });

            var delete = new MenuItem();
            delete.Header = new Views.NameHighlightedTextBlock("BranchCM.Delete", name);
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new DeleteBranch(this, branch));
                e.Handled = true;
            };

            var createBranch = new MenuItem();
            createBranch.Icon = App.CreateMenuIcon("Icons.Branch.Add");
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Click += (_, e) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new CreateBranch(this, branch));
                e.Handled = true;
            };

            var createTag = new MenuItem();
            createTag.Icon = App.CreateMenuIcon("Icons.Tag.Add");
            createTag.Header = App.Text("CreateTag");
            createTag.Click += (_, e) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new CreateTag(this, branch));
                e.Handled = true;
            };

            var archive = new MenuItem();
            archive.Icon = App.CreateMenuIcon("Icons.Archive");
            archive.Header = App.Text("Archive");
            archive.Click += (_, e) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new Archive(this, branch));
                e.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (_, e) =>
            {
                App.CopyText(name);
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
            createBranch.Click += (_, ev) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new CreateBranch(this, tag));
                ev.Handled = true;
            };

            var pushTag = new MenuItem();
            pushTag.Header = new Views.NameHighlightedTextBlock("TagCM.Push", tag.Name);
            pushTag.Icon = App.CreateMenuIcon("Icons.Push");
            pushTag.IsEnabled = _remotes.Count > 0;
            pushTag.Click += (_, ev) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new PushTag(this, tag));
                ev.Handled = true;
            };

            var deleteTag = new MenuItem();
            deleteTag.Header = new Views.NameHighlightedTextBlock("TagCM.Delete", tag.Name);
            deleteTag.Icon = App.CreateMenuIcon("Icons.Clear");
            deleteTag.Click += (_, ev) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new DeleteTag(this, tag));
                ev.Handled = true;
            };

            var archive = new MenuItem();
            archive.Icon = App.CreateMenuIcon("Icons.Archive");
            archive.Header = App.Text("Archive");
            archive.Click += (_, ev) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new Archive(this, tag));
                ev.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("TagCM.Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (_, ev) =>
            {
                App.CopyText(tag.Name);
                ev.Handled = true;
            };

            var copyMessage = new MenuItem();
            copyMessage.Header = App.Text("TagCM.CopyMessage");
            copyMessage.Icon = App.CreateMenuIcon("Icons.Copy");
            copyMessage.IsEnabled = !string.IsNullOrEmpty(tag.Message);
            copyMessage.Click += (_, ev) =>
            {
                App.CopyText(tag.Message);
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
            menu.Items.Add(copyMessage);
            return menu;
        }

        public ContextMenu CreateContextMenuForSubmodule(string submodule)
        {
            var open = new MenuItem();
            open.Header = App.Text("Submodule.Open");
            open.Icon = App.CreateMenuIcon("Icons.Folder.Open");
            open.Click += (_, ev) =>
            {
                OpenSubmodule(submodule);
                ev.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("Submodule.CopyPath");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (_, ev) =>
            {
                App.CopyText(submodule);
                ev.Handled = true;
            };

            var rm = new MenuItem();
            rm.Header = App.Text("Submodule.Remove");
            rm.Icon = App.CreateMenuIcon("Icons.Clear");
            rm.Click += (_, ev) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new DeleteSubmodule(this, submodule));
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(open);
            menu.Items.Add(copy);
            menu.Items.Add(rm);
            return menu;
        }

        public ContextMenu CreateContextMenuForWorktree(Models.Worktree worktree)
        {
            var menu = new ContextMenu();

            if (worktree.IsLocked)
            {
                var unlock = new MenuItem();
                unlock.Header = App.Text("Worktree.Unlock");
                unlock.Icon = App.CreateMenuIcon("Icons.Unlock");
                unlock.Click += (_, ev) =>
                {
                    SetWatcherEnabled(false);
                    var succ = new Commands.Worktree(_fullpath).Unlock(worktree.FullPath);
                    if (succ)
                        worktree.IsLocked = false;
                    SetWatcherEnabled(true);
                    ev.Handled = true;
                };
                menu.Items.Add(unlock);
            }
            else
            {
                var loc = new MenuItem();
                loc.Header = App.Text("Worktree.Lock");
                loc.Icon = App.CreateMenuIcon("Icons.Lock");
                loc.Click += (_, ev) =>
                {
                    SetWatcherEnabled(false);
                    var succ = new Commands.Worktree(_fullpath).Lock(worktree.FullPath);
                    if (succ)
                        worktree.IsLocked = true;
                    SetWatcherEnabled(true);
                    ev.Handled = true;
                };
                menu.Items.Add(loc);
            }

            var remove = new MenuItem();
            remove.Header = App.Text("Worktree.Remove");
            remove.Icon = App.CreateMenuIcon("Icons.Clear");
            remove.Click += (_, ev) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new RemoveWorktree(this, worktree));
                ev.Handled = true;
            };
            menu.Items.Add(remove);

            var copy = new MenuItem();
            copy.Header = App.Text("Worktree.CopyPath");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (_, e) =>
            {
                App.CopyText(worktree.FullPath);
                e.Handled = true;
            };
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(copy);

            return menu;
        }

        private MenuItem CreateMenuItemToCompareBranches(Models.Branch branch)
        {
            if (_branches.Count == 1)
                return null;

            var compare = new MenuItem();
            compare.Header = App.Text("BranchCM.CompareWithBranch");
            compare.Icon = App.CreateMenuIcon("Icons.Compare");

            foreach (var b in _branches)
            {
                if (b.FullName != branch.FullName)
                {
                    var dup = b;
                    var target = new MenuItem();
                    target.Header = b.FriendlyName;
                    target.Icon = App.CreateMenuIcon(b.IsCurrent ? "Icons.Check" : "Icons.Branch");
                    target.Click += (_, e) =>
                    {
                        App.OpenDialog(new Views.BranchCompare()
                        {
                            DataContext = new BranchCompare(_fullpath, branch, dup)
                        });
                        e.Handled = true;
                    };

                    compare.Items.Add(target);
                }
            }

            return compare;
        }

        private LauncherPage GetOwnerPage()
        {
            var launcher = App.GetLauncer();
            if (launcher == null)
                return null;

            foreach (var page in launcher.Pages)
            {
                if (page.Node.Id.Equals(_fullpath))
                    return page;
            }

            return null;
        }

        private BranchTreeNode.Builder BuildBranchTree(List<Models.Branch> branches, List<Models.Remote> remotes)
        {
            var builder = new BranchTreeNode.Builder();
            if (string.IsNullOrEmpty(_filter))
            {
                builder.SetExpandedNodes(_settings.ExpandedBranchNodesInSideBar);
                builder.Run(branches, remotes, false);

                foreach (var invalid in builder.InvalidExpandedNodes)
                    _settings.ExpandedBranchNodesInSideBar.Remove(invalid);
            }
            else
            {
                var visibles = new List<Models.Branch>();
                foreach (var b in branches)
                {
                    if (b.FullName.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                        visibles.Add(b);
                }

                builder.Run(visibles, remotes, true);
            }

            var historiesFilters = _settings.CollectHistoriesFilters();
            UpdateBranchTreeFilterMode(builder.Locals, historiesFilters);
            UpdateBranchTreeFilterMode(builder.Remotes, historiesFilters);
            return builder;
        }

        private List<Models.Tag> BuildVisibleTags()
        {
            switch (_settings.TagSortMode)
            {
                case Models.TagSortMode.CreatorDate:
                    _tags.Sort((l, r) => r.CreatorDate.CompareTo(l.CreatorDate));
                    break;
                case Models.TagSortMode.NameInAscending:
                    _tags.Sort((l, r) => Models.NumericSort.Compare(l.Name, r.Name));
                    break;
                default:
                    _tags.Sort((l, r) => Models.NumericSort.Compare(r.Name, l.Name));
                    break;
            }

            var visible = new List<Models.Tag>();
            if (string.IsNullOrEmpty(_filter))
            {
                visible.AddRange(_tags);
            }
            else
            {
                foreach (var t in _tags)
                {
                    if (t.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(t);
                }
            }

            var historiesFilters = _settings.CollectHistoriesFilters();
            UpdateTagFilterMode(historiesFilters);
            return visible;
        }

        private List<Models.Submodule> BuildVisibleSubmodules()
        {
            var visible = new List<Models.Submodule>();
            if (string.IsNullOrEmpty(_filter))
            {
                visible.AddRange(_submodules);
            }
            else
            {
                foreach (var s in _submodules)
                {
                    if (s.Path.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(s);
                }
            }
            return visible;
        }

        private void RefreshHistoriesFilters(bool refresh)
        {
            if (_settings.HistoriesFilters.Count > 0)
                HistoriesFilterMode = _settings.HistoriesFilters[0].Mode;
            else
                HistoriesFilterMode = Models.FilterMode.None;

            if (!refresh)
                return;

            var filters = _settings.CollectHistoriesFilters();
            UpdateBranchTreeFilterMode(LocalBranchTrees, filters);
            UpdateBranchTreeFilterMode(RemoteBranchTrees, filters);
            UpdateTagFilterMode(filters);

            Task.Run(RefreshCommits);
        }

        private void UpdateBranchTreeFilterMode(List<BranchTreeNode> nodes, Dictionary<string, Models.FilterMode> filters)
        {
            foreach (var node in nodes)
            {
                if (filters.TryGetValue(node.Path, out var value))
                    node.FilterMode = value;
                else
                    node.FilterMode = Models.FilterMode.None;

                if (!node.IsBranch)
                    UpdateBranchTreeFilterMode(node.Children, filters);
            }
        }

        private void UpdateTagFilterMode(Dictionary<string, Models.FilterMode> filters)
        {
            foreach (var tag in _tags)
            {
                if (filters.TryGetValue(tag.Name, out var value))
                    tag.FilterMode = value;
                else
                    tag.FilterMode = Models.FilterMode.None;
            }
        }

        private void ResetBranchTreeFilterMode(List<BranchTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                node.FilterMode = Models.FilterMode.None;
                if (!node.IsBranch)
                    ResetBranchTreeFilterMode(node.Children);
            }
        }

        private void ResetTagFilterMode()
        {
            foreach (var tag in _tags)
                tag.FilterMode = Models.FilterMode.None;
        }

        private BranchTreeNode FindBranchNode(List<BranchTreeNode> nodes, string path)
        {
            foreach (var node in nodes)
            {
                if (node.Path.Equals(path, StringComparison.Ordinal))
                    return node;

                if (path!.StartsWith(node.Path, StringComparison.Ordinal))
                {
                    var founded = FindBranchNode(node.Children, path);
                    if (founded != null)
                        return founded;
                }
            }

            return null;
        }

        private void UpdateCurrentRevisionFilesForSearchSuggestion()
        {
            _revisionFiles.Clear();

            if (_searchCommitFilterType == 3)
            {
                Task.Run(() =>
                {
                    var files = new Commands.QueryRevisionFileNames(_fullpath, "HEAD").Result();
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        if (_searchCommitFilterType != 3)
                            return;

                        _revisionFiles.AddRange(files);

                        if (!string.IsNullOrEmpty(_searchCommitFilter) && _searchCommitFilter.Length > 2 && _revisionFiles.Count > 0)
                        {
                            var suggestion = new List<string>();
                            foreach (var file in _revisionFiles)
                            {
                                if (file.Contains(_searchCommitFilter, StringComparison.OrdinalIgnoreCase) && file.Length != _searchCommitFilter.Length)
                                {
                                    suggestion.Add(file);
                                    if (suggestion.Count > 100)
                                        break;
                                }
                            }

                            SearchCommitFilterSuggestion.Clear();
                            SearchCommitFilterSuggestion.AddRange(suggestion);
                            IsSearchCommitSuggestionOpen = SearchCommitFilterSuggestion.Count > 0;
                        }
                    });
                });
            }
        }

        private void AutoFetchImpl(object sender)
        {
            if (!_settings.EnableAutoFetch || _isAutoFetching)
                return;

            var lockFile = Path.Combine(_gitDir, "index.lock");
            if (File.Exists(lockFile))
                return;

            var now = DateTime.Now;
            var desire = _lastFetchTime.AddMinutes(_settings.AutoFetchInterval);
            if (desire > now)
                return;

            var remotes = new List<string>();
            lock (_lockRemotes)
            {
                foreach (var remote in _remotes)
                    remotes.Add(remote.Name);
            }

            Dispatcher.UIThread.Invoke(() => IsAutoFetching = true);
            foreach (var remote in remotes)
                new Commands.Fetch(_fullpath, remote, false, _settings.EnablePruneOnFetch, false, null) { RaiseError = false }.Exec();
            _lastFetchTime = DateTime.Now;
            Dispatcher.UIThread.Invoke(() => IsAutoFetching = false);
        }

        private string _fullpath = string.Empty;
        private string _gitDir = string.Empty;
        private Models.RepositorySettings _settings = null;
        private Models.FilterMode _historiesFilterMode = Models.FilterMode.None;
        private bool _hasAllowedSignersFile = false;

        private Models.Watcher _watcher = null;
        private Histories _histories = null;
        private WorkingCopy _workingCopy = null;
        private StashesPage _stashesPage = null;
        private int _selectedViewIndex = 0;
        private object _selectedView = null;

        private int _localChangesCount = 0;
        private int _stashesCount = 0;

        private bool _isSearching = false;
        private bool _isSearchLoadingVisible = false;
        private bool _isSearchCommitSuggestionOpen = false;
        private int _searchCommitFilterType = 2;
        private bool _onlySearchCommitsInCurrentBranch = false;
        private string _searchCommitFilter = string.Empty;
        private List<Models.Commit> _searchedCommits = new List<Models.Commit>();
        private Models.Commit _searchResultSelectedCommit = null;
        private List<string> _revisionFiles = new List<string>();

        private string _filter = string.Empty;
        private object _lockRemotes = new object();
        private List<Models.Remote> _remotes = new List<Models.Remote>();
        private List<Models.Branch> _branches = new List<Models.Branch>();
        private Models.Branch _currentBranch = null;
        private List<BranchTreeNode> _localBranchTrees = new List<BranchTreeNode>();
        private List<BranchTreeNode> _remoteBranchTrees = new List<BranchTreeNode>();
        private List<Models.Worktree> _worktrees = new List<Models.Worktree>();
        private List<Models.Tag> _tags = new List<Models.Tag>();
        private List<Models.Tag> _visibleTags = new List<Models.Tag>();
        private List<Models.Submodule> _submodules = new List<Models.Submodule>();
        private List<Models.Submodule> _visibleSubmodules = new List<Models.Submodule>();

        private bool _isAutoFetching = false;
        private Timer _autoFetchTimer = null;
        private DateTime _lastFetchTime = DateTime.MinValue;
    }
}
