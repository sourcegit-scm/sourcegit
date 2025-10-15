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
        public bool IsBare
        {
            get;
        }

        public string FullPath
        {
            get => _fullpath;
            set
            {
                if (value != null)
                {
                    var normalized = value.Replace('\\', '/').TrimEnd('/');
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

        public Models.GitFlow GitFlow
        {
            get;
            set;
        } = new Models.GitFlow();

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

        public bool OnlyHighlightCurrentBranchInHistories
        {
            get => _settings.OnlyHighlightCurrentBranchInHistories;
            set
            {
                if (value != _settings.OnlyHighlightCurrentBranchInHistories)
                {
                    _settings.OnlyHighlightCurrentBranchInHistories = value;
                    OnPropertyChanged();
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
            private set
            {
                var oldHead = _currentBranch?.Head;
                if (SetProperty(ref _currentBranch, value))
                {
                    if (oldHead != _currentBranch.Head && _workingCopy is { UseAmend: true })
                        _workingCopy.UseAmend = false;
                }
            }
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

        public bool ShowTagsAsTree
        {
            get => Preferences.Instance.ShowTagsAsTree;
            set
            {
                if (value != Preferences.Instance.ShowTagsAsTree)
                {
                    Preferences.Instance.ShowTagsAsTree = value;
                    VisibleTags = BuildVisibleTags();
                    OnPropertyChanged();
                }
            }
        }

        public object VisibleTags
        {
            get => _visibleTags;
            private set => SetProperty(ref _visibleTags, value);
        }

        public List<Models.Submodule> Submodules
        {
            get => _submodules;
            private set => SetProperty(ref _submodules, value);
        }

        public bool ShowSubmodulesAsTree
        {
            get => Preferences.Instance.ShowSubmodulesAsTree;
            set
            {
                if (value != Preferences.Instance.ShowSubmodulesAsTree)
                {
                    Preferences.Instance.ShowSubmodulesAsTree = value;
                    VisibleSubmodules = BuildVisibleSubmodules();
                    OnPropertyChanged();
                }
            }
        }

        public object VisibleSubmodules
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

        public int LocalBranchesCount
        {
            get => _localBranchesCount;
            private set => SetProperty(ref _localBranchesCount, value);
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
                    if (value)
                    {
                        SelectedViewIndex = 0;
                        CalcWorktreeFilesForSearching();
                    }
                    else
                    {
                        SearchedCommits = new List<Models.Commit>();
                        SelectedSearchedCommit = null;
                        SearchCommitFilter = string.Empty;
                        MatchedFilesForSearching = null;
                        _requestingWorktreeFiles = false;
                        _worktreeFiles = null;
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
                    CalcWorktreeFilesForSearching();
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
                if (SetProperty(ref _searchCommitFilter, value) && IsSearchingCommitsByFilePath())
                    CalcMatchedFilesForSearching();
            }
        }

        public List<string> MatchedFilesForSearching
        {
            get => _matchedFilesForSearching;
            private set => SetProperty(ref _matchedFilesForSearching, value);
        }

        public List<Models.Commit> SearchedCommits
        {
            get => _searchedCommits;
            set => SetProperty(ref _searchedCommits, value);
        }

        public Models.Commit SelectedSearchedCommit
        {
            get => _selectedSearchedCommit;
            set
            {
                if (SetProperty(ref _selectedSearchedCommit, value) && value != null)
                    NavigateToCommit(value.SHA);
            }
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

        public bool IsSortingLocalBranchByName
        {
            get => _settings.LocalBranchSortMode == Models.BranchSortMode.Name;
        }

        public bool IsSortingRemoteBranchByName
        {
            get => _settings.RemoteBranchSortMode == Models.BranchSortMode.Name;
        }

        public bool IsSortingTagsByName
        {
            get => _settings.TagSortMode == Models.TagSortMode.Name;
        }

        public InProgressContext InProgressContext
        {
            get => _workingCopy?.InProgressContext;
        }

        public Models.BisectState BisectState
        {
            get => _bisectState;
            private set => SetProperty(ref _bisectState, value);
        }

        public bool IsBisectCommandRunning
        {
            get => _isBisectCommandRunning;
            private set => SetProperty(ref _isBisectCommandRunning, value);
        }

        public bool IsAutoFetching
        {
            get => _isAutoFetching;
            private set => SetProperty(ref _isAutoFetching, value);
        }

        public int CommitDetailActivePageIndex
        {
            get;
            set;
        } = 0;

        public AvaloniaList<CommandLog> Logs
        {
            get;
            private set;
        } = new AvaloniaList<CommandLog>();

        public Repository(bool isBare, string path, string gitDir)
        {
            IsBare = isBare;
            FullPath = path;
            GitDir = gitDir;
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
                // For worktrees, we need to watch the $GIT_COMMON_DIR instead of the $GIT_DIR.
                var gitDirForWatcher = _gitDir;
                if (_gitDir.Replace('\\', '/').IndexOf("/worktrees/", StringComparison.Ordinal) > 0)
                {
                    var commonDir = new Commands.QueryGitCommonDir(_fullpath).Result();
                    if (!string.IsNullOrEmpty(commonDir))
                        gitDirForWatcher = commonDir;
                }

                _watcher = new Models.Watcher(this, _fullpath, gitDirForWatcher);
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

            _workingCopy.CommitMessage = _settings.LastCommitMessage;
            _autoFetchTimer = new Timer(AutoFetchImpl, null, 5000, 5000);
            RefreshAll();
        }

        public void Close()
        {
            SelectedView = null; // Do NOT modify. Used to remove exists widgets for GC.Collect
            Logs.Clear();

            _settings.LastCommitMessage = _workingCopy.CommitMessage;

            var settingsSerialized = JsonSerializer.Serialize(_settings, JsonCodeGen.Default.RepositorySettings);
            try
            {
                File.WriteAllText(Path.Combine(_gitDir, "sourcegit.settings"), settingsSerialized);
            }
            catch
            {
                // Ignore
            }
            _autoFetchTimer.Dispose();
            _autoFetchTimer = null;

            _settings = null;
            _historiesFilterMode = Models.FilterMode.None;

            _watcher?.Dispose();
            _histories.Dispose();
            _workingCopy.Dispose();
            _stashesPage.Dispose();

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
            _visibleTags = null;
            _submodules.Clear();
            _visibleSubmodules = null;
            _searchedCommits.Clear();
            _selectedSearchedCommit = null;

            _requestingWorktreeFiles = false;
            _worktreeFiles = null;
            _matchedFilesForSearching = null;
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

        public bool IsGitFlowEnabled()
        {
            return GitFlow is { IsValid: true } &&
                _branches.Find(x => x.IsLocal && x.Name.Equals(GitFlow.Master, StringComparison.Ordinal)) != null &&
                _branches.Find(x => x.IsLocal && x.Name.Equals(GitFlow.Develop, StringComparison.Ordinal)) != null;
        }

        public Models.GitFlowBranchType GetGitFlowType(Models.Branch b)
        {
            if (!IsGitFlowEnabled())
                return Models.GitFlowBranchType.None;

            var name = b.Name;
            if (name.StartsWith(GitFlow.FeaturePrefix, StringComparison.Ordinal))
                return Models.GitFlowBranchType.Feature;
            if (name.StartsWith(GitFlow.ReleasePrefix, StringComparison.Ordinal))
                return Models.GitFlowBranchType.Release;
            if (name.StartsWith(GitFlow.HotfixPrefix, StringComparison.Ordinal))
                return Models.GitFlowBranchType.Hotfix;
            return Models.GitFlowBranchType.None;
        }

        public CommandLog CreateLog(string name)
        {
            var log = new CommandLog(name);
            Logs.Insert(0, log);
            return log;
        }

        public void RefreshAll()
        {
            Task.Run(RefreshCommits);
            Task.Run(RefreshBranches);
            Task.Run(RefreshTags);
            Task.Run(RefreshSubmodules);
            Task.Run(RefreshWorktrees);
            Task.Run(RefreshWorkingCopyChanges);
            Task.Run(RefreshStashes);

            Task.Run(() =>
            {
                var config = new Commands.Config(_fullpath).ListAll();
                _hasAllowedSignersFile = config.TryGetValue("gpg.ssh.allowedSignersFile", out var allowedSignersFile) && !string.IsNullOrEmpty(allowedSignersFile);

                if (config.TryGetValue("gitflow.branch.master", out var masterName))
                    GitFlow.Master = masterName;
                if (config.TryGetValue("gitflow.branch.develop", out var developName))
                    GitFlow.Develop = developName;
                if (config.TryGetValue("gitflow.prefix.feature", out var featurePrefix))
                    GitFlow.FeaturePrefix = featurePrefix;
                if (config.TryGetValue("gitflow.prefix.release", out var releasePrefix))
                    GitFlow.ReleasePrefix = releasePrefix;
                if (config.TryGetValue("gitflow.prefix.hotfix", out var hotfixPrefix))
                    GitFlow.HotfixPrefix = hotfixPrefix;
            });
        }

        public ContextMenu CreateContextMenuForExternalTools()
        {
            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

            RenderOptions.SetBitmapInterpolationMode(menu, BitmapInterpolationMode.HighQuality);
            RenderOptions.SetEdgeMode(menu, EdgeMode.Antialias);
            RenderOptions.SetTextRenderingMode(menu, TextRenderingMode.Antialias);

            var explore = new MenuItem();
            explore.Header = App.Text("Repository.Explore");
            explore.Icon = App.CreateMenuIcon("Icons.Explore");
            explore.Click += (_, e) =>
            {
                Native.OS.OpenInFileManager(_fullpath);
                e.Handled = true;
            };

            var terminal = new MenuItem();
            terminal.Header = App.Text("Repository.Terminal");
            terminal.Icon = App.CreateMenuIcon("Icons.Terminal");
            terminal.Click += (_, e) =>
            {
                Native.OS.OpenTerminal(_fullpath);
                e.Handled = true;
            };

            menu.Items.Add(explore);
            menu.Items.Add(terminal);

            var tools = Native.OS.ExternalTools;
            if (tools.Count > 0)
            {
                menu.Items.Add(new MenuItem() { Header = "-" });

                foreach (var tool in Native.OS.ExternalTools)
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
            }

            var urls = new Dictionary<string, string>();
            foreach (var r in _remotes)
            {
                if (r.TryGetVisitURL(out var visit))
                    urls.Add(r.Name, visit);
            }

            if (urls.Count > 0)
            {
                menu.Items.Add(new MenuItem() { Header = "-" });

                foreach (var (name, addr) in urls)
                {
                    var item = new MenuItem();
                    item.Header = App.Text("Repository.Visit", name);
                    item.Icon = App.CreateMenuIcon("Icons.Remotes");
                    item.Click += (_, e) =>
                    {
                        Native.OS.OpenBrowser(addr);
                        e.Handled = true;
                    };

                    menu.Items.Add(item);
                }
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

            if (_currentBranch == null)
            {
                App.RaiseException(_fullpath, "Can NOT find current branch!!!");
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
                App.RaiseException(_fullpath, "Can NOT find current branch!!!");
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

        public void ClearMatchedFilesForSearching()
        {
            MatchedFilesForSearching = null;
        }

        public void StartSearchCommits()
        {
            if (_histories == null)
                return;

            IsSearchLoadingVisible = true;
            SelectedSearchedCommit = null;
            MatchedFilesForSearching = null;

            Task.Run(() =>
            {
                var visible = null as List<Models.Commit>;
                var method = (Models.CommitSearchMethod)_searchCommitFilterType;

                if (method == Models.CommitSearchMethod.BySHA)
                {
                    var commit = new Commands.QuerySingleCommit(_fullpath, _searchCommitFilter).Result();
                    visible = commit == null ? [] : [commit];
                }
                else
                {
                    visible = new Commands.QueryCommits(_fullpath, _searchCommitFilter, method, _onlySearchCommitsInCurrentBranch).Result();
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

        public void MarkTagsDirtyManually()
        {
            if (_watcher == null)
            {
                Task.Run(RefreshTags);
                Task.Run(RefreshCommits);
            }
            else
            {
                _watcher.MarkTagDirtyManually();
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

        public void NavigateToCommit(string sha, bool isDelayMode = false)
        {
            if (isDelayMode)
            {
                _navigateToCommitDelayed = sha;
            }
            else if (_histories != null)
            {
                SelectedViewIndex = 0;
                _histories.NavigateTo(sha);
            }
        }

        public void ClearCommitMessage()
        {
            if (_workingCopy is not null)
                _workingCopy.CommitMessage = string.Empty;
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

        public void RemoveHistoriesFilter(Models.Filter filter)
        {
            if (_settings.HistoriesFilters.Remove(filter))
            {
                HistoriesFilterMode = _settings.HistoriesFilters.Count > 0 ? _settings.HistoriesFilters[0].Mode : Models.FilterMode.None;
                RefreshHistoriesFilters(true);
            }
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

                if (isLocal && !string.IsNullOrEmpty(branch.Upstream) && !branch.IsUpstreamGone)
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

        public List<Models.CustomAction> GetCustomActions(Models.CustomActionScope scope)
        {
            var actions = new List<Models.CustomAction>();

            foreach (var act in Preferences.Instance.CustomActions)
            {
                if (act.Scope == scope)
                    actions.Add(act);
            }

            foreach (var act in _settings.CustomActions)
            {
                if (act.Scope == scope)
                    actions.Add(act);
            }

            return actions;
        }

        public void Bisect(string subcmd)
        {
            IsBisectCommandRunning = true;
            SetWatcherEnabled(false);

            var log = CreateLog($"Bisect({subcmd})");
            Task.Run(() =>
            {
                var succ = new Commands.Bisect(_fullpath, subcmd).Use(log).Exec();
                log.Complete();

                Dispatcher.UIThread.Invoke(() =>
                {
                    if (!succ)
                        App.RaiseException(_fullpath, log.Content.Substring(log.Content.IndexOf('\n')).Trim());
                    else if (log.Content.Contains("is the first bad commit"))
                        App.SendNotification(_fullpath, log.Content.Substring(log.Content.IndexOf('\n')).Trim());

                    MarkBranchesDirtyManually();
                    SetWatcherEnabled(true);
                    IsBisectCommandRunning = false;
                });
            });
        }

        public bool MayHaveSubmodules()
        {
            var modulesFile = Path.Combine(_fullpath, ".gitmodules");
            var info = new FileInfo(modulesFile);
            return info.Exists && info.Length > 20;
        }

        public void RefreshBranches()
        {
            var branches = new Commands.QueryBranches(_fullpath).Result(out var localBranchesCount);
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
                LocalBranchesCount = localBranchesCount;

                if (_workingCopy != null)
                    _workingCopy.HasRemotes = remotes.Count > 0;

                var hasPendingPullOrPush = CurrentBranch?.TrackStatus.IsVisible ?? false;
                GetOwnerPage()?.ChangeDirtyState(Models.DirtyState.HasPendingPullOrPush, !hasPendingPullOrPush);
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
            builder.Append($"-{Preferences.Instance.MaxHistoryCommits} ");

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
                builder.Append("--branches --remotes --tags HEAD");
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

                    BisectState = _histories.UpdateBisectInfo();

                    if (!string.IsNullOrEmpty(_navigateToCommitDelayed))
                        NavigateToCommit(_navigateToCommitDelayed);
                }

                _navigateToCommitDelayed = string.Empty;
            });
        }

        public void RefreshSubmodules()
        {
            if (!MayHaveSubmodules())
            {
                if (_submodules.Count > 0)
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        Submodules = [];
                        VisibleSubmodules = BuildVisibleSubmodules();
                    });
                }

                return;
            }

            var submodules = new Commands.QuerySubmodules(_fullpath).Result();
            _watcher?.SetSubmodules(submodules);

            Dispatcher.UIThread.Invoke(() =>
            {
                bool hasChanged = _submodules.Count != submodules.Count;
                if (!hasChanged)
                {
                    var old = new Dictionary<string, Models.Submodule>();
                    foreach (var module in _submodules)
                        old.Add(module.Path, module);

                    foreach (var module in submodules)
                    {
                        if (!old.TryGetValue(module.Path, out var exist))
                        {
                            hasChanged = true;
                            break;
                        }

                        hasChanged = !exist.SHA.Equals(module.SHA, StringComparison.Ordinal) ||
                            !exist.URL.Equals(module.URL, StringComparison.Ordinal) ||
                            exist.Status != module.Status;

                        if (hasChanged)
                            break;
                    }
                }

                if (hasChanged)
                {
                    Submodules = submodules;
                    VisibleSubmodules = BuildVisibleSubmodules();
                }
            });
        }

        public void RefreshWorkingCopyChanges()
        {
            if (IsBare)
                return;

            var changes = new Commands.QueryLocalChanges(_fullpath, _settings.IncludeUntrackedInLocalChanges).Result();
            if (_workingCopy == null)
                return;

            changes.Sort((l, r) => Models.NumericSort.Compare(l.Path, r.Path));
            _workingCopy.SetData(changes);

            Dispatcher.UIThread.Invoke(() =>
            {
                LocalChangesCount = changes.Count;
                OnPropertyChanged(nameof(InProgressContext));
                GetOwnerPage()?.ChangeDirtyState(Models.DirtyState.HasLocalChanges, changes.Count == 0);
            });
        }

        public void RefreshStashes()
        {
            if (IsBare)
                return;

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
                App.RaiseException(_fullpath, "Git cannot create a branch before your first commit.");
                return;
            }

            if (CanCreatePopup())
                ShowPopup(new CreateBranch(this, _currentBranch));
        }

        public void CheckoutBranch(Models.Branch branch)
        {
            if (branch.IsLocal)
            {
                var worktree = _worktrees.Find(x => x.Branch.Equals(branch.FullName, StringComparison.Ordinal));
                if (worktree != null)
                {
                    OpenWorktree(worktree);
                    return;
                }
            }

            if (IsBare)
                return;

            if (!CanCreatePopup())
                return;

            if (branch.IsLocal)
            {
                if (_localChangesCount > 0 || _submodules.Count > 0)
                    ShowPopup(new Checkout(this, branch.Name));
                else
                    ShowAndStartPopup(new Checkout(this, branch.Name));
            }
            else
            {
                foreach (var b in _branches)
                {
                    if (b.IsLocal &&
                        b.Upstream.Equals(branch.FullName, StringComparison.Ordinal) &&
                        b.TrackStatus.Ahead.Count == 0)
                    {
                        if (b.TrackStatus.Behind.Count > 0)
                            ShowPopup(new CheckoutAndFastForward(this, b, branch));
                        else if (!b.IsCurrent)
                            CheckoutBranch(b);

                        return;
                    }
                }

                ShowPopup(new CreateBranch(this, branch));
            }
        }

        public void DeleteBranch(Models.Branch branch)
        {
            if (CanCreatePopup())
                ShowPopup(new DeleteBranch(this, branch));
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
                App.RaiseException(_fullpath, "Git cannot create a branch before your first commit.");
                return;
            }

            if (CanCreatePopup())
                ShowPopup(new CreateTag(this, _currentBranch));
        }

        public void DeleteTag(Models.Tag tag)
        {
            if (CanCreatePopup())
                ShowPopup(new DeleteTag(this, tag));
        }

        public void AddRemote()
        {
            if (CanCreatePopup())
                ShowPopup(new AddRemote(this));
        }

        public void DeleteRemote(Models.Remote remote)
        {
            if (CanCreatePopup())
                ShowPopup(new DeleteRemote(this, remote));
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
            var selfPage = GetOwnerPage();
            if (selfPage == null)
                return;

            var root = Path.GetFullPath(Path.Combine(_fullpath, submodule));
            var normalizedPath = root.Replace('\\', '/').TrimEnd('/');

            var node = Preferences.Instance.FindNode(normalizedPath);
            if (node == null)
            {
                node = new RepositoryNode()
                {
                    Id = normalizedPath,
                    Name = Path.GetFileName(normalizedPath),
                    Bookmark = selfPage.Node.Bookmark,
                    IsRepository = true,
                };
            }

            App.GetLauncher().OpenRepositoryInTab(node, null);
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
            var node = Preferences.Instance.FindNode(worktree.FullPath);
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

            App.GetLauncher()?.OpenRepositoryInTab(node, null);
        }

        public List<Models.OpenAIService> GetPreferredOpenAIServices()
        {
            var services = Preferences.Instance.OpenAIServices;
            if (services == null || services.Count == 0)
                return [];

            if (services.Count == 1)
                return [services[0]];

            var preferred = _settings.PreferredOpenAIService;
            var all = new List<Models.OpenAIService>();
            foreach (var service in services)
            {
                if (service.Name.Equals(preferred, StringComparison.Ordinal))
                    return [service];

                all.Add(service);
            }

            return all;
        }

        public ContextMenu CreateContextMenuForGitFlow()
        {
            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

            if (IsGitFlowEnabled())
            {
                var startFeature = new MenuItem();
                startFeature.Header = App.Text("GitFlow.StartFeature");
                startFeature.Icon = App.CreateMenuIcon("Icons.GitFlow.Feature");
                startFeature.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new GitFlowStart(this, Models.GitFlowBranchType.Feature));
                    e.Handled = true;
                };

                var startRelease = new MenuItem();
                startRelease.Header = App.Text("GitFlow.StartRelease");
                startRelease.Icon = App.CreateMenuIcon("Icons.GitFlow.Release");
                startRelease.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new GitFlowStart(this, Models.GitFlowBranchType.Release));
                    e.Handled = true;
                };

                var startHotfix = new MenuItem();
                startHotfix.Header = App.Text("GitFlow.StartHotfix");
                startHotfix.Icon = App.CreateMenuIcon("Icons.GitFlow.Hotfix");
                startHotfix.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new GitFlowStart(this, Models.GitFlowBranchType.Hotfix));
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
                    if (_currentBranch == null)
                    {
                        App.RaiseException(_fullpath, "Git flow init failed: No branch found!!!");
                    }
                    else if (CanCreatePopup())
                    {
                        ShowPopup(new InitGitFlow(this));
                    }

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
                        App.ShowWindow(new LFSLocks(this, _remotes[0].Name), true);
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
                            App.ShowWindow(new LFSLocks(this, remoteName), true);
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
                    var log = CreateLog("Install LFS");
                    var succ = new Commands.LFS(_fullpath).Install(log);
                    if (succ)
                        App.SendNotification(_fullpath, $"LFS enabled successfully!");

                    log.Complete();
                    e.Handled = true;
                };
                menu.Items.Add(install);
            }

            return menu;
        }

        public ContextMenu CreateContextMenuForCustomAction()
        {
            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

            var actions = GetCustomActions(Models.CustomActionScope.Repository);
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
                            ShowAndStartPopup(new ExecuteCustomAction(this, dup));

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

        public ContextMenu CreateContextMenuForHistoriesPage()
        {
            var layout = new MenuItem();
            layout.Header = App.Text("Repository.HistoriesLayout");
            layout.IsEnabled = false;

            var isHorizontal = Preferences.Instance.UseTwoColumnsLayoutInHistories;
            var horizontal = new MenuItem();
            horizontal.Header = App.Text("Repository.HistoriesLayout.Horizontal");
            if (isHorizontal)
                horizontal.Icon = App.CreateMenuIcon("Icons.Check");
            horizontal.Click += (_, ev) =>
            {
                Preferences.Instance.UseTwoColumnsLayoutInHistories = true;
                ev.Handled = true;
            };

            var vertical = new MenuItem();
            vertical.Header = App.Text("Repository.HistoriesLayout.Vertical");
            if (!isHorizontal)
                vertical.Icon = App.CreateMenuIcon("Icons.Check");
            vertical.Click += (_, ev) =>
            {
                Preferences.Instance.UseTwoColumnsLayoutInHistories = false;
                ev.Handled = true;
            };

            var order = new MenuItem();
            order.Header = App.Text("Repository.HistoriesOrder");
            order.IsEnabled = false;

            var dateOrder = new MenuItem();
            dateOrder.Header = App.Text("Repository.HistoriesOrder.ByDate");
            dateOrder.SetValue(Views.MenuItemExtension.CommandProperty, "--date-order");
            if (!_settings.EnableTopoOrderInHistories)
                dateOrder.Icon = App.CreateMenuIcon("Icons.Check");
            dateOrder.Click += (_, ev) =>
            {
                if (_settings.EnableTopoOrderInHistories)
                {
                    _settings.EnableTopoOrderInHistories = false;
                    Task.Run(RefreshCommits);
                }

                ev.Handled = true;
            };

            var topoOrder = new MenuItem();
            topoOrder.Header = App.Text("Repository.HistoriesOrder.Topo");
            topoOrder.SetValue(Views.MenuItemExtension.CommandProperty, "--top-order");
            if (_settings.EnableTopoOrderInHistories)
                topoOrder.Icon = App.CreateMenuIcon("Icons.Check");
            topoOrder.Click += (_, ev) =>
            {
                if (!_settings.EnableTopoOrderInHistories)
                {
                    _settings.EnableTopoOrderInHistories = true;
                    Task.Run(RefreshCommits);
                }

                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(layout);
            menu.Items.Add(horizontal);
            menu.Items.Add(vertical);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(order);
            menu.Items.Add(dateOrder);
            menu.Items.Add(topoOrder);
            return menu;
        }

        public void DiscardAllChanges()
        {
            if (CanCreatePopup())
                ShowPopup(new Discard(this));
        }

        public void ClearStashes()
        {
            if (CanCreatePopup())
                ShowPopup(new ClearStashes(this));
        }

        public ContextMenu CreateContextMenuForLocalBranch(Models.Branch branch)
        {
            var menu = new ContextMenu();

            var push = new MenuItem();
            push.Header = App.Text("BranchCM.Push", branch.Name);
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
                if (!IsBare)
                {
                    if (!string.IsNullOrEmpty(branch.Upstream))
                    {
                        var upstream = branch.Upstream.Substring(13);
                        var fastForward = new MenuItem();
                        fastForward.Header = App.Text("BranchCM.FastForward", upstream);
                        fastForward.Icon = App.CreateMenuIcon("Icons.FastForward");
                        fastForward.IsEnabled = branch.TrackStatus.Ahead.Count == 0;
                        fastForward.Click += (_, e) =>
                        {
                            var b = _branches.Find(x => x.FriendlyName == upstream);
                            if (b == null)
                                return;

                            if (CanCreatePopup())
                                ShowAndStartPopup(new Merge(this, b, branch.Name, true));

                            e.Handled = true;
                        };

                        var pull = new MenuItem();
                        pull.Header = App.Text("BranchCM.Pull", upstream);
                        pull.Icon = App.CreateMenuIcon("Icons.Pull");
                        pull.Click += (_, e) =>
                        {
                            if (CanCreatePopup())
                                ShowPopup(new Pull(this, null));
                            e.Handled = true;
                        };

                        menu.Items.Add(fastForward);
                        menu.Items.Add(new MenuItem() { Header = "-" });
                        menu.Items.Add(pull);
                    }
                }

                menu.Items.Add(push);

                menu.Items.Add(new MenuItem() { Header = "-" });
                // Add Open Pull Request menu item
                var openPR = new MenuItem();
                openPR.Header = App.Text("BranchCM.OpenPullRequest", branch.Name);
                openPR.Icon = App.CreateMenuIcon("Icons.OpenWith");
                openPR.IsEnabled = _remotes.Count > 0;
                openPR.Click += (_, e) =>
                {
                    OpenPullRequest(branch);
                    e.Handled = true;
                };
                menu.Items.Add(openPR);
            }
            else
            {
                if (!IsBare)
                {
                    var checkout = new MenuItem();
                    checkout.Header = App.Text("BranchCM.Checkout", branch.Name);
                    checkout.Icon = App.CreateMenuIcon("Icons.Check");
                    checkout.Click += (_, e) =>
                    {
                        CheckoutBranch(branch);
                        e.Handled = true;
                    };
                    menu.Items.Add(checkout);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                var worktree = _worktrees.Find(x => x.Branch == branch.FullName);
                var upstream = _branches.Find(x => x.FullName == branch.Upstream);
                if (upstream != null && worktree == null)
                {
                    var fastForward = new MenuItem();
                    fastForward.Header = App.Text("BranchCM.FastForward", upstream.FriendlyName);
                    fastForward.Icon = App.CreateMenuIcon("Icons.FastForward");
                    fastForward.IsEnabled = branch.TrackStatus.Ahead.Count == 0;
                    fastForward.Click += (_, e) =>
                    {
                        if (CanCreatePopup())
                            ShowAndStartPopup(new ResetWithoutCheckout(this, branch, upstream));
                        e.Handled = true;
                    };
                    menu.Items.Add(fastForward);

                    var fetchInto = new MenuItem();
                    fetchInto.Header = App.Text("BranchCM.FetchInto", upstream.FriendlyName, branch.Name);
                    fetchInto.Icon = App.CreateMenuIcon("Icons.Fetch");
                    fetchInto.IsEnabled = branch.TrackStatus.Ahead.Count == 0;
                    fetchInto.Click += (_, e) =>
                    {
                        if (CanCreatePopup())
                            ShowAndStartPopup(new FetchInto(this, branch, upstream));
                        e.Handled = true;
                    };

                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(fetchInto);
                }

                menu.Items.Add(push);

                if (!IsBare)
                {
                    var merge = new MenuItem();
                    merge.Header = App.Text("BranchCM.Merge", branch.Name, _currentBranch.Name);
                    merge.Icon = App.CreateMenuIcon("Icons.Merge");
                    merge.Click += (_, e) =>
                    {
                        if (CanCreatePopup())
                            ShowPopup(new Merge(this, branch, _currentBranch.Name, false));
                        e.Handled = true;
                    };

                    var rebase = new MenuItem();
                    rebase.Header = App.Text("BranchCM.Rebase", _currentBranch.Name, branch.Name);
                    rebase.Icon = App.CreateMenuIcon("Icons.Rebase");
                    rebase.Click += (_, e) =>
                    {
                        if (CanCreatePopup())
                            ShowPopup(new Rebase(this, _currentBranch, branch));
                        e.Handled = true;
                    };

                    menu.Items.Add(merge);
                    menu.Items.Add(rebase);
                }

                if (worktree == null)
                {
                    var selectedCommit = (_histories?.DetailContext as CommitDetail)?.Commit;
                    if (selectedCommit != null && !selectedCommit.SHA.Equals(branch.Head, StringComparison.Ordinal))
                    {
                        var move = new MenuItem();
                        move.Header = App.Text("BranchCM.ResetToSelectedCommit", branch.Name, selectedCommit.SHA.Substring(0, 10));
                        move.Icon = App.CreateMenuIcon("Icons.Reset");
                        move.Click += (_, e) =>
                        {
                            if (CanCreatePopup())
                                ShowPopup(new ResetWithoutCheckout(this, branch, selectedCommit));
                            e.Handled = true;
                        };
                        menu.Items.Add(new MenuItem() { Header = "-" });
                        menu.Items.Add(move);
                    }
                }

                var compareWithCurrent = new MenuItem();
                compareWithCurrent.Header = App.Text("BranchCM.CompareWithCurrent", _currentBranch.Name);
                compareWithCurrent.Icon = App.CreateMenuIcon("Icons.Compare");
                compareWithCurrent.Click += (_, _) =>
                {
                    App.ShowWindow(new BranchCompare(_fullpath, branch, _currentBranch), false);
                };
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(compareWithCurrent);

                if (_localChangesCount > 0)
                {
                    var compareWithWorktree = new MenuItem();
                    compareWithWorktree.Header = App.Text("BranchCM.CompareWithWorktree");
                    compareWithWorktree.Icon = App.CreateMenuIcon("Icons.Compare");
                    compareWithWorktree.Click += (_, _) =>
                    {
                        SelectedSearchedCommit = null;

                        if (_histories != null)
                        {
                            var target = new Commands.QuerySingleCommit(_fullpath, branch.Head).Result();
                            _histories.AutoSelectedCommit = null;
                            _histories.DetailContext = new RevisionCompare(_fullpath, target, null);
                        }
                    };
                    menu.Items.Add(compareWithWorktree);
                }
            }

            if (!IsBare)
            {
                var type = GetGitFlowType(branch);
                if (type != Models.GitFlowBranchType.None)
                {
                    var finish = new MenuItem();
                    finish.Header = App.Text("BranchCM.Finish", branch.Name);
                    finish.Icon = App.CreateMenuIcon("Icons.GitFlow");
                    finish.Click += (_, e) =>
                    {
                        if (CanCreatePopup())
                            ShowPopup(new GitFlowFinish(this, branch, type));
                        e.Handled = true;
                    };
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(finish);
                }
            }

            var rename = new MenuItem();
            rename.Header = App.Text("BranchCM.Rename", branch.Name);
            rename.Icon = App.CreateMenuIcon("Icons.Rename");
            rename.Click += (_, e) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new RenameBranch(this, branch));
                e.Handled = true;
            };

            var delete = new MenuItem();
            delete.Header = App.Text("BranchCM.Delete", branch.Name);
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
            TryToAddCustomActionsToBranchContextMenu(menu, branch);

            if (!IsBare)
            {
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
            checkout.Header = App.Text("BranchCM.Checkout", name);
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
                pull.Header = App.Text("BranchCM.PullInto", name, _currentBranch.Name);
                pull.Icon = App.CreateMenuIcon("Icons.Pull");
                pull.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new Pull(this, branch));
                    e.Handled = true;
                };

                var merge = new MenuItem();
                merge.Header = App.Text("BranchCM.Merge", name, _currentBranch.Name);
                merge.Icon = App.CreateMenuIcon("Icons.Merge");
                merge.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowPopup(new Merge(this, branch, _currentBranch.Name, false));
                    e.Handled = true;
                };

                var rebase = new MenuItem();
                rebase.Header = App.Text("BranchCM.Rebase", _currentBranch.Name, name);
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

            var compareWithHead = new MenuItem();
            compareWithHead.Header = App.Text("BranchCM.CompareWithHead");
            compareWithHead.Icon = App.CreateMenuIcon("Icons.Compare");
            compareWithHead.Click += (_, _) =>
            {
                App.ShowWindow(new BranchCompare(_fullpath, branch, _currentBranch), false);
            };
            menu.Items.Add(compareWithHead);

            if (_localChangesCount > 0)
            {
                var compareWithWorktree = new MenuItem();
                compareWithWorktree.Header = App.Text("BranchCM.CompareWithWorktree");
                compareWithWorktree.Icon = App.CreateMenuIcon("Icons.Compare");
                compareWithWorktree.Click += (_, _) =>
                {
                    SelectedSearchedCommit = null;

                    if (_histories != null)
                    {
                        var target = new Commands.QuerySingleCommit(_fullpath, branch.Head).Result();
                        _histories.AutoSelectedCommit = null;
                        _histories.DetailContext = new RevisionCompare(_fullpath, target, null);
                    }
                };
                menu.Items.Add(compareWithWorktree);
            }
            menu.Items.Add(new MenuItem() { Header = "-" });

            var delete = new MenuItem();
            delete.Header = App.Text("BranchCM.Delete", name);
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
            TryToAddCustomActionsToBranchContextMenu(menu, branch);
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
            pushTag.Header = App.Text("TagCM.Push", tag.Name);
            pushTag.Icon = App.CreateMenuIcon("Icons.Push");
            pushTag.IsEnabled = _remotes.Count > 0;
            pushTag.Click += (_, ev) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new PushTag(this, tag));
                ev.Handled = true;
            };

            var deleteTag = new MenuItem();
            deleteTag.Header = App.Text("TagCM.Delete", tag.Name);
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

        public ContextMenu CreateContextMenuForBranchSortMode(bool local)
        {
            var mode = local ? _settings.LocalBranchSortMode : _settings.RemoteBranchSortMode;
            var changeMode = new Action<Models.BranchSortMode>(m =>
            {
                if (local)
                {
                    _settings.LocalBranchSortMode = m;
                    OnPropertyChanged(nameof(IsSortingLocalBranchByName));
                }
                else
                {
                    _settings.RemoteBranchSortMode = m;
                    OnPropertyChanged(nameof(IsSortingRemoteBranchByName));
                }

                var builder = BuildBranchTree(_branches, _remotes);
                LocalBranchTrees = builder.Locals;
                RemoteBranchTrees = builder.Remotes;
            });

            var byNameAsc = new MenuItem();
            byNameAsc.Header = App.Text("Repository.BranchSort.ByName");
            if (mode == Models.BranchSortMode.Name)
                byNameAsc.Icon = App.CreateMenuIcon("Icons.Check");
            byNameAsc.Click += (_, ev) =>
            {
                if (mode != Models.BranchSortMode.Name)
                    changeMode(Models.BranchSortMode.Name);

                ev.Handled = true;
            };

            var byCommitterDate = new MenuItem();
            byCommitterDate.Header = App.Text("Repository.BranchSort.ByCommitterDate");
            if (mode == Models.BranchSortMode.CommitterDate)
                byCommitterDate.Icon = App.CreateMenuIcon("Icons.Check");
            byCommitterDate.Click += (_, ev) =>
            {
                if (mode != Models.BranchSortMode.CommitterDate)
                    changeMode(Models.BranchSortMode.CommitterDate);

                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
            menu.Items.Add(byNameAsc);
            menu.Items.Add(byCommitterDate);
            return menu;
        }

        public ContextMenu CreateContextMenuForTagSortMode()
        {
            var mode = _settings.TagSortMode;
            var changeMode = new Action<Models.TagSortMode>(m =>
            {
                if (_settings.TagSortMode != m)
                {
                    _settings.TagSortMode = m;
                    OnPropertyChanged(nameof(IsSortingTagsByName));
                    VisibleTags = BuildVisibleTags();
                }
            });

            var byCreatorDate = new MenuItem();
            byCreatorDate.Header = App.Text("Repository.Tags.OrderByCreatorDate");
            if (mode == Models.TagSortMode.CreatorDate)
                byCreatorDate.Icon = App.CreateMenuIcon("Icons.Check");
            byCreatorDate.Click += (_, ev) =>
            {
                changeMode(Models.TagSortMode.CreatorDate);
                ev.Handled = true;
            };

            var byName = new MenuItem();
            byName.Header = App.Text("Repository.Tags.OrderByName");
            if (mode == Models.TagSortMode.Name)
                byName.Icon = App.CreateMenuIcon("Icons.Check");
            byName.Click += (_, ev) =>
            {
                changeMode(Models.TagSortMode.Name);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
            menu.Items.Add(byCreatorDate);
            menu.Items.Add(byName);
            return menu;
        }

        public ContextMenu CreateContextMenuForSubmodule(Models.Submodule submodule)
        {
            var open = new MenuItem();
            open.Header = App.Text("Submodule.Open");
            open.Icon = App.CreateMenuIcon("Icons.Folder.Open");
            open.IsEnabled = submodule.Status != Models.SubmoduleStatus.NotInited;
            open.Click += (_, ev) =>
            {
                OpenSubmodule(submodule.Path);
                ev.Handled = true;
            };

            var deinit = new MenuItem();
            deinit.Header = App.Text("Submodule.Deinit");
            deinit.Icon = App.CreateMenuIcon("Icons.Undo");
            deinit.IsEnabled = submodule.Status != Models.SubmoduleStatus.NotInited;
            deinit.Click += (_, ev) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new DeinitSubmodule(this, submodule.Path));
                ev.Handled = true;
            };

            var rm = new MenuItem();
            rm.Header = App.Text("Submodule.Remove");
            rm.Icon = App.CreateMenuIcon("Icons.Clear");
            rm.Click += (_, ev) =>
            {
                if (CanCreatePopup())
                    ShowPopup(new DeleteSubmodule(this, submodule.Path));
                ev.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("Submodule.CopyPath");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (_, ev) =>
            {
                App.CopyText(submodule.Path);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(open);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(deinit);
            menu.Items.Add(rm);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(copy);
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
                    var log = CreateLog("Unlock Worktree");
                    var succ = new Commands.Worktree(_fullpath).Use(log).Unlock(worktree.FullPath);
                    if (succ)
                        worktree.IsLocked = false;
                    log.Complete();
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
                    var log = CreateLog("Lock Worktree");
                    var succ = new Commands.Worktree(_fullpath).Use(log).Lock(worktree.FullPath);
                    if (succ)
                        worktree.IsLocked = true;
                    log.Complete();
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

        private LauncherPage GetOwnerPage()
        {
            var launcher = App.GetLauncher();
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
            var builder = new BranchTreeNode.Builder(_settings.LocalBranchSortMode, _settings.RemoteBranchSortMode);
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

        private object BuildVisibleTags()
        {
            switch (_settings.TagSortMode)
            {
                case Models.TagSortMode.CreatorDate:
                    _tags.Sort((l, r) => r.CreatorDate.CompareTo(l.CreatorDate));
                    break;
                default:
                    _tags.Sort((l, r) => Models.NumericSort.Compare(l.Name, r.Name));
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

            if (Preferences.Instance.ShowTagsAsTree)
                return TagCollectionAsTree.Build(visible, _visibleTags as TagCollectionAsTree);
            else
                return new TagCollectionAsList() { Tags = visible };
        }

        private object BuildVisibleSubmodules()
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

            if (Preferences.Instance.ShowSubmodulesAsTree)
                return SubmoduleCollectionAsTree.Build(visible, _visibleSubmodules as SubmoduleCollectionAsTree);
            else
                return new SubmoduleCollectionAsList() { Submodules = visible };
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
                node.FilterMode = filters.GetValueOrDefault(node.Path, Models.FilterMode.None);

                if (!node.IsBranch)
                    UpdateBranchTreeFilterMode(node.Children, filters);
            }
        }

        private void UpdateTagFilterMode(Dictionary<string, Models.FilterMode> filters)
        {
            foreach (var tag in _tags)
            {
                tag.FilterMode = filters.GetValueOrDefault(tag.Name, Models.FilterMode.None);
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

                if (path.StartsWith(node.Path, StringComparison.Ordinal))
                {
                    var founded = FindBranchNode(node.Children, path);
                    if (founded != null)
                        return founded;
                }
            }

            return null;
        }

        private void TryToAddCustomActionsToBranchContextMenu(ContextMenu menu, Models.Branch branch)
        {
            var actions = GetCustomActions(Models.CustomActionScope.Branch);
            if (actions.Count == 0)
                return;

            var custom = new MenuItem();
            custom.Header = App.Text("BranchCM.CustomAction");
            custom.Icon = App.CreateMenuIcon("Icons.Action");

            foreach (var action in actions)
            {
                var dup = action;
                var item = new MenuItem();
                item.Icon = App.CreateMenuIcon("Icons.Action");
                item.Header = dup.Name;
                item.Click += (_, e) =>
                {
                    if (CanCreatePopup())
                        ShowAndStartPopup(new ExecuteCustomAction(this, dup, branch));

                    e.Handled = true;
                };

                custom.Items.Add(item);
            }

            menu.Items.Add(custom);
            menu.Items.Add(new MenuItem() { Header = "-" });
        }

        private bool IsSearchingCommitsByFilePath()
        {
            return _isSearching && _searchCommitFilterType == (int)Models.CommitSearchMethod.ByFile;
        }

        private void CalcWorktreeFilesForSearching()
        {
            if (!IsSearchingCommitsByFilePath())
            {
                _requestingWorktreeFiles = false;
                _worktreeFiles = null;
                MatchedFilesForSearching = null;
                GC.Collect();
                return;
            }

            if (_requestingWorktreeFiles)
                return;

            _requestingWorktreeFiles = true;

            Task.Run(() =>
            {
                _worktreeFiles = new Commands.QueryRevisionFileNames(_fullpath, "HEAD").Result();
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (IsSearchingCommitsByFilePath() && _requestingWorktreeFiles)
                        CalcMatchedFilesForSearching();

                    _requestingWorktreeFiles = false;
                });
            });
        }

        private void CalcMatchedFilesForSearching()
        {
            if (_worktreeFiles == null || _worktreeFiles.Count == 0 || _searchCommitFilter.Length < 3)
            {
                MatchedFilesForSearching = null;
                return;
            }

            var matched = new List<string>();
            foreach (var file in _worktreeFiles)
            {
                if (file.Contains(_searchCommitFilter, StringComparison.OrdinalIgnoreCase) && file.Length != _searchCommitFilter.Length)
                {
                    matched.Add(file);
                    if (matched.Count > 100)
                        break;
                }
            }

            MatchedFilesForSearching = matched;
        }

        private void AutoFetchImpl(object sender)
        {
            try
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
                    new Commands.Fetch(_fullpath, remote, false, false) { RaiseError = false }.Exec();
                _lastFetchTime = DateTime.Now;
                Dispatcher.UIThread.Invoke(() => IsAutoFetching = false);
            }
            catch
            {
                // DO nothing, but prevent `System.AggregateException`
            }
        }

        private string GetDefaultBranch()
        {
            var main = _branches.Find(b => b.Name == "main");
            var master = _branches.Find(b => b.Name == "master");
            var other = _branches.Find(b => b.Name != _currentBranch.Name);
            return main != null ? main.Name : (master != null ? master.Name : other.Name);
        }

        private void OpenPullRequest(Models.Branch branch)
        {
            var autoSelectedRemote = null as Models.Remote;
            if (!string.IsNullOrEmpty(CurrentBranch.Upstream))
            {
                var remoteNameEndIdx = CurrentBranch.Upstream.IndexOf('/', 13);
                if (remoteNameEndIdx > 0)
                {
                    var remoteName = CurrentBranch.Upstream.Substring(13, remoteNameEndIdx - 13);
                    autoSelectedRemote = Remotes.Find(x => x.Name == remoteName);
                }
            }

            var _selectedRemote = null as Models.Remote;

            if (autoSelectedRemote == null)
            {
                var remote = null as Models.Remote;
                if (!string.IsNullOrEmpty(Settings.DefaultRemote))
                    remote = Remotes.Find(x => x.Name == Settings.DefaultRemote);
                _selectedRemote = remote ?? Remotes[0];
            }
            else
            {
                _selectedRemote = autoSelectedRemote;
            }

            if (_selectedRemote == null || !_selectedRemote.TryGetVisitURL(out string baseUrl))
                return;

            var defaultBranch = GetDefaultBranch();
            string prUrl = "";
            if (baseUrl.Contains("github.com"))
            {
                prUrl = $"{baseUrl}/compare/{defaultBranch}...{branch.Name}";
            }
            else if (baseUrl.Contains("gitlab.com"))
            {
                prUrl = $"{baseUrl}/-/merge_requests/new?merge_request[source_branch]={branch.Name}&merge_request[target_branch]={defaultBranch}";
            }
            else if (baseUrl.Contains("gitee.com"))
            {
                prUrl = $"{baseUrl}/pulls/new?source={branch.Name}&target={defaultBranch}";
            }
            else if (baseUrl.Contains("bitbucket.org"))
            {
                prUrl = $"{baseUrl}/pull-requests/new?source={branch.Name}&dest={defaultBranch}";
            }
            else if (baseUrl.Contains("gitea.com"))
            {
                prUrl = $"{baseUrl}/pulls/new?source={branch.Name}&target={defaultBranch}";
            }
            else if (baseUrl.Contains("azure.com"))
            {
                prUrl = $"{baseUrl}/pullrequestcreate?sourceRef={branch.Name}&targetRef={defaultBranch}";
            }
            else
            {
                prUrl = $"{baseUrl}";
            }

            if (!string.IsNullOrEmpty(prUrl))
            {
                Native.OS.OpenBrowser(prUrl);
            }
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

        private int _localBranchesCount = 0;
        private int _localChangesCount = 0;
        private int _stashesCount = 0;

        private bool _isSearching = false;
        private bool _isSearchLoadingVisible = false;
        private int _searchCommitFilterType = (int)Models.CommitSearchMethod.ByMessage;
        private bool _onlySearchCommitsInCurrentBranch = false;
        private string _searchCommitFilter = string.Empty;
        private List<Models.Commit> _searchedCommits = new List<Models.Commit>();
        private Models.Commit _selectedSearchedCommit = null;
        private bool _requestingWorktreeFiles = false;
        private List<string> _worktreeFiles = null;
        private List<string> _matchedFilesForSearching = null;

        private string _filter = string.Empty;
        private readonly Lock _lockRemotes = new();
        private List<Models.Remote> _remotes = new List<Models.Remote>();
        private List<Models.Branch> _branches = new List<Models.Branch>();
        private Models.Branch _currentBranch = null;
        private List<BranchTreeNode> _localBranchTrees = new List<BranchTreeNode>();
        private List<BranchTreeNode> _remoteBranchTrees = new List<BranchTreeNode>();
        private List<Models.Worktree> _worktrees = new List<Models.Worktree>();
        private List<Models.Tag> _tags = new List<Models.Tag>();
        private object _visibleTags = null;
        private List<Models.Submodule> _submodules = new List<Models.Submodule>();
        private object _visibleSubmodules = null;

        private bool _isAutoFetching = false;
        private Timer _autoFetchTimer = null;
        private DateTime _lastFetchTime = DateTime.MinValue;

        private Models.BisectState _bisectState = Models.BisectState.None;
        private bool _isBisectCommandRunning = false;

        private string _navigateToCommitDelayed = string.Empty;
    }
}
