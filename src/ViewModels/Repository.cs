using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Collections;
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
            get;
        }

        public string GitDir
        {
            get;
        }

        public Models.RepositorySettings Settings
        {
            get => _settings;
        }

        public Models.GitFlow GitFlow
        {
            get;
            set;
        } = new();

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
                    SelectedView = value switch
                    {
                        1 => _workingCopy,
                        2 => _stashesPage,
                        _ => _histories,
                    };
                }
            }
        }

        public object SelectedView
        {
            get => _selectedView;
            set => SetProperty(ref _selectedView, value);
        }

        public bool EnableTopoOrderInHistories
        {
            get => _settings.EnableTopoOrderInHistories;
            set
            {
                if (value != _settings.EnableTopoOrderInHistories)
                {
                    _settings.EnableTopoOrderInHistories = value;
                    RefreshCommits();
                }
            }
        }

        public Models.HistoryShowFlags HistoryShowFlags
        {
            get => _settings.HistoryShowFlags;
            private set
            {
                if (value != _settings.HistoryShowFlags)
                {
                    _settings.HistoryShowFlags = value;
                    RefreshCommits();
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
                if (SetProperty(ref _currentBranch, value) && value != null)
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
                    RefreshWorkingCopyChanges();
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
            set
            {
                _settings.LocalBranchSortMode = value ? Models.BranchSortMode.Name : Models.BranchSortMode.CommitterDate;
                OnPropertyChanged();

                var builder = BuildBranchTree(_branches, _remotes);
                LocalBranchTrees = builder.Locals;
                RemoteBranchTrees = builder.Remotes;
            }
        }

        public bool IsSortingRemoteBranchByName
        {
            get => _settings.RemoteBranchSortMode == Models.BranchSortMode.Name;
            set
            {
                _settings.RemoteBranchSortMode = value ? Models.BranchSortMode.Name : Models.BranchSortMode.CommitterDate;
                OnPropertyChanged();

                var builder = BuildBranchTree(_branches, _remotes);
                LocalBranchTrees = builder.Locals;
                RemoteBranchTrees = builder.Remotes;
            }
        }

        public bool IsSortingTagsByName
        {
            get => _settings.TagSortMode == Models.TagSortMode.Name;
            set
            {
                _settings.TagSortMode = value ? Models.TagSortMode.Name : Models.TagSortMode.CreatorDate;
                OnPropertyChanged();
                VisibleTags = BuildVisibleTags();
            }
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

        public AvaloniaList<Models.IssueTracker> IssueTrackers
        {
            get;
        } = [];

        public AvaloniaList<CommandLog> Logs
        {
            get;
        } = [];

        public Repository(bool isBare, string path, string gitDir)
        {
            IsBare = isBare;
            FullPath = path.Replace('\\', '/').TrimEnd('/');
            GitDir = gitDir.Replace('\\', '/').TrimEnd('/');

            var commonDirFile = Path.Combine(GitDir, "commondir");
            _isWorktree = GitDir.IndexOf("/worktrees/", StringComparison.Ordinal) > 0 &&
                          File.Exists(commonDirFile);

            if (_isWorktree)
            {
                var commonDir = File.ReadAllText(commonDirFile).Trim();
                if (!Path.IsPathRooted(commonDir))
                    commonDir = new DirectoryInfo(Path.Combine(GitDir, commonDir)).FullName;

                _gitCommonDir = commonDir;
            }
            else
            {
                _gitCommonDir = GitDir;
            }
        }

        public void Open()
        {
            var settingsFile = Path.Combine(_gitCommonDir, "sourcegit.settings");
            if (File.Exists(settingsFile))
            {
                try
                {
                    using var stream = File.OpenRead(settingsFile);
                    _settings = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.RepositorySettings);
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
                _watcher = new Models.Watcher(this, FullPath, _gitCommonDir);
            }
            catch (Exception ex)
            {
                App.RaiseException(string.Empty, $"Failed to start watcher for repository: '{FullPath}'. You may need to press 'F5' to refresh repository manually!\n\nReason: {ex.Message}");
            }

            if (_settings.HistoriesFilters.Count > 0)
                _historiesFilterMode = _settings.HistoriesFilters[0].Mode;
            else
                _historiesFilterMode = Models.FilterMode.None;

            _histories = new Histories(this);
            _workingCopy = new WorkingCopy(this) { CommitMessage = _settings.LastCommitMessage };
            _stashesPage = new StashesPage(this);

            if (Preferences.Instance.ShowLocalChangesByDefault)
            {
                _selectedView = _workingCopy;
                _selectedViewIndex = 1;
            }
            else
            {
                _selectedView = _histories;
                _selectedViewIndex = 0;
            }

            _lastFetchTime = DateTime.Now;
            _autoFetchTimer = new Timer(FetchInBackground, null, 5000, 5000);
            RefreshAll();
        }

        public void Close()
        {
            SelectedView = null; // Do NOT modify. Used to remove exists widgets for GC.Collect
            Logs.Clear();

            if (!_isWorktree)
            {
                _settings.LastCommitMessage = _workingCopy.CommitMessage;
                using var stream = File.Create(Path.Combine(_gitCommonDir, "sourcegit.settings"));
                JsonSerializer.Serialize(stream, _settings, JsonCodeGen.Default.RepositorySettings);
            }

            if (_cancellationRefreshBranches is { IsCancellationRequested: false })
                _cancellationRefreshBranches.Cancel();
            if (_cancellationRefreshTags is { IsCancellationRequested: false })
                _cancellationRefreshTags.Cancel();
            if (_cancellationRefreshWorkingCopyChanges is { IsCancellationRequested: false })
                _cancellationRefreshWorkingCopyChanges.Cancel();
            if (_cancellationRefreshCommits is { IsCancellationRequested: false })
                _cancellationRefreshCommits.Cancel();
            if (_cancellationRefreshStashes is { IsCancellationRequested: false })
                _cancellationRefreshStashes.Cancel();

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

        public async Task ShowAndStartPopupAsync(Popup popup)
        {
            var page = GetOwnerPage();
            page.Popup = popup;

            if (popup.CanStartDirectly())
                await page.ProcessPopupAsync();
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

        public bool IsLFSEnabled()
        {
            var path = Path.Combine(FullPath, ".git", "hooks", "pre-push");
            if (!File.Exists(path))
                return false;

            var content = File.ReadAllText(path);
            return content.Contains("git lfs pre-push");
        }

        public async Task InstallLFSAsync()
        {
            var log = CreateLog("Install LFS");
            var succ = await new Commands.LFS(FullPath).Use(log).InstallAsync();
            if (succ)
                App.SendNotification(FullPath, "LFS enabled successfully!");

            log.Complete();
        }

        public async Task<bool> TrackLFSFileAsync(string pattern, bool isFilenameMode)
        {
            var log = CreateLog("Track LFS");
            var succ = await new Commands.LFS(FullPath)
                .Use(log)
                .TrackAsync(pattern, isFilenameMode);

            if (succ)
                App.SendNotification(FullPath, $"Tracking successfully! Pattern: {pattern}");

            log.Complete();
            return succ;
        }

        public async Task<bool> LockLFSFileAsync(string remote, string path)
        {
            var log = CreateLog("Lock LFS File");
            var succ = await new Commands.LFS(FullPath)
                .Use(log)
                .LockAsync(remote, path);

            if (succ)
                App.SendNotification(FullPath, $"Lock file successfully! File: {path}");

            log.Complete();
            return succ;
        }

        public async Task<bool> UnlockLFSFileAsync(string remote, string path, bool force, bool notify)
        {
            var log = CreateLog("Unlock LFS File");
            var succ = await new Commands.LFS(FullPath)
                .Use(log)
                .UnlockAsync(remote, path, force);

            if (succ && notify)
                App.SendNotification(FullPath, $"Unlock file successfully! File: {path}");

            log.Complete();
            return succ;
        }

        public CommandLog CreateLog(string name)
        {
            var log = new CommandLog(name);
            Logs.Insert(0, log);
            return log;
        }

        public async Task<Models.IssueTracker> AddIssueTrackerAsync(string name, string regex, string url)
        {
            var rule = new Models.IssueTracker()
            {
                IsShared = false,
                Name = name,
                RegexString = regex,
                URLTemplate = url,
            };

            var succ = await CreateIssueTrackerCommand(false).AddAsync(rule);
            if (succ)
            {
                IssueTrackers.Add(rule);
                return rule;
            }

            return null;
        }

        public async Task RemoveIssueTrackerAsync(Models.IssueTracker rule)
        {
            var succ = await CreateIssueTrackerCommand(rule.IsShared).RemoveAsync(rule);
            if (succ)
                IssueTrackers.Remove(rule);
        }

        public async Task ChangeIssueTrackerShareModeAsync(Models.IssueTracker rule)
        {
            await CreateIssueTrackerCommand(!rule.IsShared).RemoveAsync(rule);
            await CreateIssueTrackerCommand(rule.IsShared).AddAsync(rule);
        }

        public void RefreshAll()
        {
            RefreshCommits();
            RefreshBranches();
            RefreshTags();
            RefreshSubmodules();
            RefreshWorktrees();
            RefreshWorkingCopyChanges();
            RefreshStashes();

            Task.Run(async () =>
            {
                var issuetrackers = new List<Models.IssueTracker>();
                await CreateIssueTrackerCommand(true).ReadAllAsync(issuetrackers, true).ConfigureAwait(false);
                await CreateIssueTrackerCommand(false).ReadAllAsync(issuetrackers, false).ConfigureAwait(false);
                Dispatcher.UIThread.Post(() =>
                {
                    IssueTrackers.Clear();
                    IssueTrackers.AddRange(issuetrackers);
                });

                var config = await new Commands.Config(FullPath).ReadAllAsync().ConfigureAwait(false);
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

        public async Task FetchAsync(bool autoStart)
        {
            if (!CanCreatePopup())
                return;

            if (_remotes.Count == 0)
            {
                App.RaiseException(FullPath, "No remotes added to this repository!!!");
                return;
            }

            if (autoStart)
                await ShowAndStartPopupAsync(new Fetch(this));
            else
                ShowPopup(new Fetch(this));
        }

        public async Task PullAsync(bool autoStart)
        {
            if (IsBare || !CanCreatePopup())
                return;

            if (_remotes.Count == 0)
            {
                App.RaiseException(FullPath, "No remotes added to this repository!!!");
                return;
            }

            if (_currentBranch == null)
            {
                App.RaiseException(FullPath, "Can NOT find current branch!!!");
                return;
            }

            var pull = new Pull(this, null);
            if (autoStart && pull.SelectedBranch != null)
                await ShowAndStartPopupAsync(pull);
            else
                ShowPopup(pull);
        }

        public async Task PushAsync(bool autoStart)
        {
            if (!CanCreatePopup())
                return;

            if (_remotes.Count == 0)
            {
                App.RaiseException(FullPath, "No remotes added to this repository!!!");
                return;
            }

            if (_currentBranch == null)
            {
                App.RaiseException(FullPath, "Can NOT find current branch!!!");
                return;
            }

            if (autoStart)
                await ShowAndStartPopupAsync(new Push(this, null));
            else
                ShowPopup(new Push(this, null));
        }

        public void ApplyPatch()
        {
            if (CanCreatePopup())
                ShowPopup(new Apply(this));
        }

        public async Task ExecCustomActionAsync(Models.CustomAction action, object scopeTarget)
        {
            if (!CanCreatePopup())
                return;

            var popup = new ExecuteCustomAction(this, action, scopeTarget);
            if (action.Controls.Count == 0)
                await ShowAndStartPopupAsync(popup);
            else
                ShowPopup(popup);
        }

        public async Task CleanupAsync()
        {
            if (CanCreatePopup())
                await ShowAndStartPopupAsync(new Cleanup(this));
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

            Task.Run(async () =>
            {
                var visible = new List<Models.Commit>();
                var method = (Models.CommitSearchMethod)_searchCommitFilterType;

                if (method == Models.CommitSearchMethod.BySHA)
                {
                    var isCommitSHA = await new Commands.IsCommitSHA(FullPath, _searchCommitFilter)
                        .GetResultAsync()
                        .ConfigureAwait(false);

                    if (isCommitSHA)
                    {
                        var commit = await new Commands.QuerySingleCommit(FullPath, _searchCommitFilter)
                            .GetResultAsync()
                            .ConfigureAwait(false);
                        visible.Add(commit);
                    }
                }
                else
                {
                    visible = await new Commands.QueryCommits(FullPath, _searchCommitFilter, method, _onlySearchCommitsInCurrentBranch)
                        .GetResultAsync()
                        .ConfigureAwait(false);
                }

                Dispatcher.UIThread.Post(() =>
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
            _watcher?.MarkBranchUpdated();
            RefreshBranches();
            RefreshCommits();
            RefreshWorkingCopyChanges();
            RefreshWorktrees();
        }

        public void MarkTagsDirtyManually()
        {
            _watcher?.MarkTagUpdated();
            RefreshTags();
            RefreshCommits();
        }

        public void MarkWorkingCopyDirtyManually()
        {
            _watcher?.MarkWorkingCopyUpdated();
            RefreshWorkingCopyChanges();
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

        public Models.Commit GetSelectedCommitInHistory()
        {
            return (_histories?.DetailContext as CommitDetail)?.Commit;
        }

        public void ClearHistoriesFilter()
        {
            _settings.HistoriesFilters.Clear();
            HistoriesFilterMode = Models.FilterMode.None;

            ResetBranchTreeFilterMode(LocalBranchTrees);
            ResetBranchTreeFilterMode(RemoteBranchTrees);
            ResetTagFilterMode();
            RefreshCommits();
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

        public async Task StashAllAsync(bool autoStart)
        {
            if (_workingCopy != null)
                await _workingCopy.StashAllAsync(autoStart);
        }

        public async Task SkipMergeAsync()
        {
            if (_workingCopy != null)
                await _workingCopy.SkipMergeAsync();
        }

        public async Task AbortMergeAsync()
        {
            if (_workingCopy != null)
                await _workingCopy.AbortMergeAsync();
        }

        public List<(Models.CustomAction, CustomActionContextMenuLabel)> GetCustomActions(Models.CustomActionScope scope)
        {
            var actions = new List<(Models.CustomAction, CustomActionContextMenuLabel)>();

            foreach (var act in Preferences.Instance.CustomActions)
            {
                if (act.Scope == scope)
                    actions.Add((act, new CustomActionContextMenuLabel(act.Name, true)));
            }

            foreach (var act in _settings.CustomActions)
            {
                if (act.Scope == scope)
                    actions.Add((act, new CustomActionContextMenuLabel(act.Name, false)));
            }

            return actions;
        }

        public async Task ExecBisectCommandAsync(string subcmd)
        {
            IsBisectCommandRunning = true;
            SetWatcherEnabled(false);

            var log = CreateLog($"Bisect({subcmd})");

            var succ = await new Commands.Bisect(FullPath, subcmd).Use(log).ExecAsync();
            log.Complete();

            var head = await new Commands.QueryRevisionByRefName(FullPath, "HEAD").GetResultAsync();
            if (!succ)
                App.RaiseException(FullPath, log.Content.Substring(log.Content.IndexOf('\n')).Trim());
            else if (log.Content.Contains("is the first bad commit"))
                App.SendNotification(FullPath, log.Content.Substring(log.Content.IndexOf('\n')).Trim());

            MarkBranchesDirtyManually();
            NavigateToCommit(head, true);
            SetWatcherEnabled(true);
            IsBisectCommandRunning = false;
        }

        public bool MayHaveSubmodules()
        {
            var modulesFile = Path.Combine(FullPath, ".gitmodules");
            var info = new FileInfo(modulesFile);
            return info.Exists && info.Length > 20;
        }

        public void RefreshBranches()
        {
            if (_cancellationRefreshBranches is { IsCancellationRequested: false })
                _cancellationRefreshBranches.Cancel();

            _cancellationRefreshBranches = new CancellationTokenSource();
            var token = _cancellationRefreshBranches.Token;

            Task.Run(async () =>
            {
                var branches = await new Commands.QueryBranches(FullPath).GetResultAsync().ConfigureAwait(false);
                var remotes = await new Commands.QueryRemotes(FullPath).GetResultAsync().ConfigureAwait(false);
                var builder = BuildBranchTree(branches, remotes);

                Dispatcher.UIThread.Invoke(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    Remotes = remotes;
                    Branches = branches;
                    CurrentBranch = branches.Find(x => x.IsCurrent);
                    LocalBranchTrees = builder.Locals;
                    RemoteBranchTrees = builder.Remotes;

                    var localBranchesCount = 0;
                    foreach (var b in branches)
                    {
                        if (b.IsLocal && !b.IsDetachedHead)
                            localBranchesCount++;
                    }
                    LocalBranchesCount = localBranchesCount;

                    if (_workingCopy != null)
                        _workingCopy.HasRemotes = remotes.Count > 0;

                    var hasPendingPullOrPush = CurrentBranch?.TrackStatus.IsVisible ?? false;
                    GetOwnerPage()?.ChangeDirtyState(Models.DirtyState.HasPendingPullOrPush, !hasPendingPullOrPush);
                });
            }, token);
        }

        public void RefreshWorktrees()
        {
            Task.Run(async () =>
            {
                var worktrees = await new Commands.Worktree(FullPath).ReadAllAsync().ConfigureAwait(false);
                if (worktrees.Count == 0)
                {
                    Dispatcher.UIThread.Invoke(() => Worktrees = worktrees);
                    return;
                }

                var cleaned = new List<Models.Worktree>();
                foreach (var worktree in worktrees)
                {
                    if (worktree.FullPath.Equals(FullPath, StringComparison.Ordinal) ||
                        worktree.FullPath.Equals(GitDir, StringComparison.Ordinal))
                        continue;

                    cleaned.Add(worktree);
                }

                Dispatcher.UIThread.Invoke(() => Worktrees = cleaned);
            });
        }

        public void RefreshTags()
        {
            if (_cancellationRefreshTags is { IsCancellationRequested: false })
                _cancellationRefreshTags.Cancel();

            _cancellationRefreshTags = new CancellationTokenSource();
            var token = _cancellationRefreshTags.Token;

            Task.Run(async () =>
            {
                var tags = await new Commands.QueryTags(FullPath).GetResultAsync().ConfigureAwait(false);
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    Tags = tags;
                    VisibleTags = BuildVisibleTags();
                });
            }, token);
        }

        public void RefreshCommits()
        {
            if (_cancellationRefreshCommits is { IsCancellationRequested: false })
                _cancellationRefreshCommits.Cancel();

            _cancellationRefreshCommits = new CancellationTokenSource();
            var token = _cancellationRefreshCommits.Token;

            Task.Run(async () =>
            {
                await Dispatcher.UIThread.InvokeAsync(() => _histories.IsLoading = true);

                var builder = new StringBuilder();
                builder.Append($"-{Preferences.Instance.MaxHistoryCommits} ");

                if (_settings.EnableTopoOrderInHistories)
                    builder.Append("--topo-order ");
                else
                    builder.Append("--date-order ");

                if (_settings.HistoryShowFlags.HasFlag(Models.HistoryShowFlags.Reflog))
                    builder.Append("--reflog ");

                if (_settings.HistoryShowFlags.HasFlag(Models.HistoryShowFlags.FirstParentOnly))
                    builder.Append("--first-parent ");

                if (_settings.HistoryShowFlags.HasFlag(Models.HistoryShowFlags.SimplifyByDecoration))
                    builder.Append("--simplify-by-decoration ");

                var filters = _settings.BuildHistoriesFilter();
                if (string.IsNullOrEmpty(filters))
                    builder.Append("--branches --remotes --tags HEAD");
                else
                    builder.Append(filters);

                var commits = await new Commands.QueryCommits(FullPath, builder.ToString()).GetResultAsync().ConfigureAwait(false);
                var graph = Models.CommitGraph.Parse(commits, _settings.HistoryShowFlags.HasFlag(Models.HistoryShowFlags.FirstParentOnly));

                Dispatcher.UIThread.Invoke(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

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
            }, token);
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

            Task.Run(async () =>
            {
                var submodules = await new Commands.QuerySubmodules(FullPath).GetResultAsync().ConfigureAwait(false);
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
                                         !exist.Branch.Equals(module.Branch, StringComparison.Ordinal) ||
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
            });
        }

        public void RefreshWorkingCopyChanges()
        {
            if (IsBare)
                return;

            if (_cancellationRefreshWorkingCopyChanges is { IsCancellationRequested: false })
                _cancellationRefreshWorkingCopyChanges.Cancel();

            _cancellationRefreshWorkingCopyChanges = new CancellationTokenSource();
            var token = _cancellationRefreshWorkingCopyChanges.Token;

            Task.Run(async () =>
            {
                var changes = await new Commands.QueryLocalChanges(FullPath, _settings.IncludeUntrackedInLocalChanges)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                if (_workingCopy == null || token.IsCancellationRequested)
                    return;

                changes.Sort((l, r) => Models.NumericSort.Compare(l.Path, r.Path));
                _workingCopy.SetData(changes, token);

                Dispatcher.UIThread.Invoke(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    LocalChangesCount = changes.Count;
                    OnPropertyChanged(nameof(InProgressContext));
                    GetOwnerPage()?.ChangeDirtyState(Models.DirtyState.HasLocalChanges, changes.Count == 0);
                });
            }, token);
        }

        public void RefreshStashes()
        {
            if (IsBare)
                return;

            if (_cancellationRefreshStashes is { IsCancellationRequested: false })
                _cancellationRefreshStashes.Cancel();

            _cancellationRefreshStashes = new CancellationTokenSource();
            var token = _cancellationRefreshStashes.Token;

            Task.Run(async () =>
            {
                var stashes = await new Commands.QueryStashes(FullPath).GetResultAsync().ConfigureAwait(false);
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    if (_stashesPage != null)
                        _stashesPage.Stashes = stashes;

                    StashesCount = stashes.Count;
                });
            }, token);
        }

        public void ToggleHistoryShowFlag(Models.HistoryShowFlags flag)
        {
            if (_settings.HistoryShowFlags.HasFlag(flag))
                HistoryShowFlags -= flag;
            else
                HistoryShowFlags |= flag;
        }

        public void CreateNewBranch()
        {
            if (_currentBranch == null)
            {
                App.RaiseException(FullPath, "Git cannot create a branch before your first commit.");
                return;
            }

            if (CanCreatePopup())
                ShowPopup(new CreateBranch(this, _currentBranch));
        }

        public async Task CheckoutBranchAsync(Models.Branch branch)
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
                    await ShowAndStartPopupAsync(new Checkout(this, branch.Name));
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
                            await CheckoutBranchAsync(b);

                        return;
                    }
                }

                ShowPopup(new CreateBranch(this, branch));
            }
        }

        public async Task CheckoutTagAsync(Models.Tag tag)
        {
            var c = await new Commands.QuerySingleCommit(FullPath, tag.SHA).GetResultAsync();
            if (c != null && _histories != null)
                await _histories.CheckoutBranchByCommitAsync(c);
        }

        public async Task CompareBranchWithWorktreeAsync(Models.Branch branch)
        {
            if (_histories != null)
            {
                SelectedSearchedCommit = null;

                var target = await new Commands.QuerySingleCommit(FullPath, branch.Head).GetResultAsync();
                _histories.AutoSelectedCommit = null;
                _histories.DetailContext = new RevisionCompare(FullPath, target, null);
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
                App.RaiseException(FullPath, "Git cannot create a branch before your first commit.");
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
                ShowPopup(new UpdateSubmodules(this, null));
        }

        public void OpenSubmodule(string submodule)
        {
            var selfPage = GetOwnerPage();
            if (selfPage == null)
                return;

            var root = Path.GetFullPath(Path.Combine(FullPath, submodule));
            var normalizedPath = root.Replace('\\', '/').TrimEnd('/');

            var node = Preferences.Instance.FindNode(normalizedPath) ??
                new RepositoryNode
                {
                    Id = normalizedPath,
                    Name = Path.GetFileName(normalizedPath),
                    Bookmark = selfPage.Node.Bookmark,
                    IsRepository = true,
                };

            App.GetLauncher().OpenRepositoryInTab(node, null);
        }

        public void AddWorktree()
        {
            if (CanCreatePopup())
                ShowPopup(new AddWorktree(this));
        }

        public async Task PruneWorktreesAsync()
        {
            if (CanCreatePopup())
                await ShowAndStartPopupAsync(new PruneWorktrees(this));
        }

        public void OpenWorktree(Models.Worktree worktree)
        {
            var node = Preferences.Instance.FindNode(worktree.FullPath) ??
                new RepositoryNode
                {
                    Id = worktree.FullPath,
                    Name = Path.GetFileName(worktree.FullPath),
                    Bookmark = 0,
                    IsRepository = true,
                };

            App.GetLauncher().OpenRepositoryInTab(node, null);
        }

        public async Task LockWorktreeAsync(Models.Worktree worktree)
        {
            SetWatcherEnabled(false);
            var log = CreateLog("Lock Worktree");
            var succ = await new Commands.Worktree(FullPath).Use(log).LockAsync(worktree.FullPath);
            if (succ)
                worktree.IsLocked = true;
            log.Complete();
            SetWatcherEnabled(true);
        }

        public async Task UnlockWorktreeAsync(Models.Worktree worktree)
        {
            SetWatcherEnabled(false);
            var log = CreateLog("Unlock Worktree");
            var succ = await new Commands.Worktree(FullPath).Use(log).UnlockAsync(worktree.FullPath);
            if (succ)
                worktree.IsLocked = false;
            log.Complete();
            SetWatcherEnabled(true);
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

        public async Task<bool> SaveCommitAsPatchAsync(Models.Commit commit, string folder, int index = 0)
        {
            var ignoredChars = new HashSet<char> { '/', '\\', ':', ',', '*', '?', '\"', '<', '>', '|', '`', '$', '^', '%', '[', ']', '+', '-' };
            var builder = new StringBuilder();
            builder.Append(index.ToString("D4"));
            builder.Append('-');

            var chars = commit.Subject.ToCharArray();
            var len = 0;
            foreach (var c in chars)
            {
                if (!ignoredChars.Contains(c))
                {
                    if (c == ' ' || c == '\t')
                        builder.Append('-');
                    else
                        builder.Append(c);

                    len++;

                    if (len >= 48)
                        break;
                }
            }
            builder.Append(".patch");

            var saveTo = Path.Combine(folder, builder.ToString());
            var log = CreateLog("Save Commit as Patch");
            var succ = await new Commands.FormatPatch(FullPath, commit.SHA, saveTo).Use(log).ExecAsync();
            log.Complete();
            return succ;
        }

        private LauncherPage GetOwnerPage()
        {
            var launcher = App.GetLauncher();
            if (launcher == null)
                return null;

            foreach (var page in launcher.Pages)
            {
                if (page.Node.Id.Equals(FullPath))
                    return page;
            }

            return null;
        }

        private Commands.IssueTracker CreateIssueTrackerCommand(bool shared)
        {
            return new Commands.IssueTracker(FullPath, shared ? $"{FullPath}/.issuetracker" : null);
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
            {
                var tree = TagCollectionAsTree.Build(visible, _visibleTags as TagCollectionAsTree);
                foreach (var node in tree.Tree)
                    node.UpdateFilterMode(historiesFilters);
                return tree;
            }
            else
            {
                var list = new TagCollectionAsList(visible);
                foreach (var item in list.TagItems)
                    item.FilterMode = historiesFilters.GetValueOrDefault(item.Tag.Name, Models.FilterMode.None);
                return list;
            }
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
            RefreshCommits();
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
            if (VisibleTags is TagCollectionAsTree tree)
            {
                foreach (var node in tree.Tree)
                    node.UpdateFilterMode(filters);
            }
            else if (VisibleTags is TagCollectionAsList list)
            {
                foreach (var item in list.TagItems)
                    item.FilterMode = filters.GetValueOrDefault(item.Tag.Name, Models.FilterMode.None);
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
            if (VisibleTags is TagCollectionAsTree tree)
            {
                var filters = new Dictionary<string, Models.FilterMode>();
                foreach (var node in tree.Tree)
                    node.UpdateFilterMode(filters);
            }
            else if (VisibleTags is TagCollectionAsList list)
            {
                foreach (var item in list.TagItems)
                    item.FilterMode = Models.FilterMode.None;
            }
        }

        private BranchTreeNode FindBranchNode(List<BranchTreeNode> nodes, string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

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

        private bool IsSearchingCommitsByFilePath()
        {
            return _isSearching && _searchCommitFilterType == (int)Models.CommitSearchMethod.ByPath;
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

            Task.Run(async () =>
            {
                _worktreeFiles = await new Commands.QueryRevisionFileNames(FullPath, "HEAD")
                    .GetResultAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
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

        private void FetchInBackground(object sender)
        {
            Dispatcher.UIThread.Invoke(async Task () =>
            {
                if (_settings is not { EnableAutoFetch: true })
                    return;

                if (!CanCreatePopup())
                {
                    _lastFetchTime = DateTime.Now;
                    return;
                }

                var lockFile = Path.Combine(GitDir, "index.lock");
                if (File.Exists(lockFile))
                    return;

                var now = DateTime.Now;
                var desire = _lastFetchTime.AddMinutes(_settings.AutoFetchInterval);
                if (desire > now)
                    return;

                var remotes = new List<string>();
                foreach (var r in _remotes)
                    remotes.Add(r.Name);

                IsAutoFetching = true;

                if (_settings.FetchAllRemotes)
                {
                    foreach (var remote in remotes)
                        await new Commands.Fetch(FullPath, remote, false, false) { RaiseError = false }.RunAsync();
                }
                else if (remotes.Count > 0)
                {
                    var remote = string.IsNullOrEmpty(_settings.DefaultRemote) ?
                        remotes.Find(x => x.Equals(_settings.DefaultRemote, StringComparison.Ordinal)) :
                        remotes[0];

                    await new Commands.Fetch(FullPath, remote, false, false) { RaiseError = false }.RunAsync();
                }

                _lastFetchTime = DateTime.Now;
                IsAutoFetching = false;
            });
        }

        private readonly bool _isWorktree = false;
        private readonly string _gitCommonDir = null;
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
        private List<Models.Commit> _searchedCommits = [];
        private Models.Commit _selectedSearchedCommit = null;
        private bool _requestingWorktreeFiles = false;
        private List<string> _worktreeFiles = null;
        private List<string> _matchedFilesForSearching = null;

        private string _filter = string.Empty;
        private List<Models.Remote> _remotes = [];
        private List<Models.Branch> _branches = [];
        private Models.Branch _currentBranch = null;
        private List<BranchTreeNode> _localBranchTrees = [];
        private List<BranchTreeNode> _remoteBranchTrees = [];
        private List<Models.Worktree> _worktrees = [];
        private List<Models.Tag> _tags = [];
        private object _visibleTags = null;
        private List<Models.Submodule> _submodules = [];
        private object _visibleSubmodules = null;
        private string _navigateToCommitDelayed = string.Empty;

        private bool _isAutoFetching = false;
        private Timer _autoFetchTimer = null;
        private DateTime _lastFetchTime = DateTime.MinValue;

        private Models.BisectState _bisectState = Models.BisectState.None;
        private bool _isBisectCommandRunning = false;

        private CancellationTokenSource _cancellationRefreshBranches = null;
        private CancellationTokenSource _cancellationRefreshTags = null;
        private CancellationTokenSource _cancellationRefreshWorkingCopyChanges = null;
        private CancellationTokenSource _cancellationRefreshCommits = null;
        private CancellationTokenSource _cancellationRefreshStashes = null;
    }
}
