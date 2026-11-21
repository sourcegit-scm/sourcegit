using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class SubmoduleCommitDetail : ObservableObject
    {
        public string Path { get; init; }
        public Models.SubmoduleHistory Section { get; init; }
        public string PointerStatus
        {
            get => _pointerStatus;
            set => SetProperty(ref _pointerStatus, value);
        }
        private string _pointerStatus = string.Empty;
        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }
        private string _text = string.Empty;
    }

    public class SubmoduleComparisonDetail : SubmoduleCommitDetail
    {
        public List<string> PreviousPointers { get; init; }
        public string NewPointer { get; init; }
        public Action<string> Compare { get; init; }
        public string SelectedPrevious
        {
            get => _selectedPrevious;
            set
            {
                if (SetProperty(ref _selectedPrevious, value) && value != null)
                    Compare(value);
            }
        }
        public SubmoduleComparisonDetail(string selected) => _selectedPrevious = selected;
        private string _selectedPrevious;
    }

    public class SubmoduleNavigationItem
    {
        public string Label { get; init; }
        public string Separator { get; init; }
        public string GraphKey { get; init; }
        public bool IsAvailable { get; init; }
    }

    public partial class Histories
    {
        private Models.SubmoduleHistory NavigationSection =>
            (_detailContext as SubmoduleCommitDetail)?.Section ?? _selectedCommits.FirstOrDefault()?.SubmoduleHistory;

        public bool CanNavigateSubmoduleUp => NavigationSection != null;

        public List<SubmoduleNavigationItem> SubmoduleNavigation
        {
            get
            {
                var chain = new List<Models.SubmoduleHistory>();
                for (var section = NavigationSection; section != null; section = section.ParentHistory)
                    chain.Add(section);
                chain.Reverse();
                var result = new List<SubmoduleNavigationItem>();
                if (chain.Count == 0)
                    return result;
                var rootName = System.IO.Path.GetFileName(_repo.FullPath.TrimEnd('/', '\\'));
                Add(rootName, chain[0].OwnerKey);
                for (var i = 0; i < chain.Count; i++)
                {
                    var section = chain[i];
                    var selected = _selectedCommits.FirstOrDefault();
                    var target = i + 1 < chain.Count ? chain[i + 1].OwnerKey :
                        selected?.SubmoduleHistory?.Key == section.Key ? selected.GraphKey :
                        section.GraphKey(section.NewSHA ?? section.Previous.Keys.FirstOrDefault());
                    Add(section.LocalPath ?? section.Path, target);
                }
                return result;

                void Add(string label, string key) => result.Add(new SubmoduleNavigationItem
                {
                    Label = label,
                    Separator = result.Count == 0 ? string.Empty : "→",
                    GraphKey = key,
                    IsAvailable = _commits.Any(c => c.GraphKey == key),
                });
            }
        }

        private void NotifySubmoduleNavigation()
        {
            OnPropertyChanged(nameof(SubmoduleNavigation));
            OnPropertyChanged(nameof(CanNavigateSubmoduleUp));
        }

        public void NavigateSubmoduleUp()
        {
            if (NavigationSection != null)
                NavigateSubmoduleTo(NavigationSection.OwnerKey);
        }

        public void NavigateSubmoduleTo(string key)
        {
            var target = _commits.Find(c => c.GraphKey == key);
            if (target == null)
                return;
            CancelSubmoduleHistory();
            _ignoreSelectionChange = true;
            try
            {
                SelectedCommits = [target];
            }
            finally
            {
                _ignoreSelectionChange = false;
            }
            SubmoduleStatus = string.Empty;
            SubmoduleChanges = _knownSubmodules.Values.Where(s => s.OwnerKey == key).ToList();
            if (target.IsSubmodulePlaceholder)
                _ = CompareSubmoduleAsync(target.SubmoduleHistory, target.SubmoduleHistory.Previous.Keys.FirstOrDefault());
            else if (target.SubmoduleHistory != null)
                _ = ShowSubmoduleCommitAsync(target, false);
            else
                DetailContext = new CommitDetail(_repo, _commitDetailSharedData) { Commit = target };
            NotifySubmoduleNavigation();
        }

        public List<Models.SubmoduleHistory> SubmoduleChanges
        {
            get => _submoduleChanges;
            private set => SetProperty(ref _submoduleChanges, value);
        }

        public string SubmoduleStatus
        {
            get => _submoduleStatus;
            private set => SetProperty(ref _submoduleStatus, value);
        }

        public string SubmoduleExpansionCount => $"{_expandedSubmodules.Count} / {Preferences.Instance.MaxExpandedSubmoduleHistories}";

        public List<Models.SubmoduleHistory> ExpandedSubmodules => new(_expandedSubmodules);
        public bool HasExpandedSubmodules => _expandedSubmodules.Count > 0;

        public List<Models.SubmoduleHistory> GetSubmoduleHistoryChoices() =>
            _knownSubmodules.Values.Where(s => _commits.Any(c => c.GraphKey == s.HeaderKey)).ToList();

        private void NotifyExpandedSubmodules()
        {
            OnPropertyChanged(nameof(SubmoduleExpansionCount));
            OnPropertyChanged(nameof(ExpandedSubmodules));
            OnPropertyChanged(nameof(HasExpandedSubmodules));
        }

        public void CollapseSubmoduleHistory(Models.SubmoduleHistory section)
        {
            // Keep the first real commit visible while hiding the rest of the area.
            if (!IsSubmoduleExpanded(section))
                return;
            CancelSubmoduleHistory();
            _expandedSubmodules.RemoveAll(x => IsWithin(x, section));
            SubmoduleStatus = string.Empty;
            RebuildSubmoduleGraph();
            SelectSubmoduleHeader(section);
        }

        public bool IsSubmoduleExpanded(Models.SubmoduleHistory section) => _expandedSubmodules.Any(x => x.Key == section.Key);

        public void CancelSubmoduleHistory()
        {
            _expandingCommit = null;
            _pointerStatusRequest?.Cancel();
            _pointerStatusRequest?.Dispose();
            _pointerStatusRequest = null;
            _submoduleRequest?.Cancel();
            _submoduleRequest?.Dispose();
            _submoduleRequest = null;
        }

        public void CollapseSubmoduleHistories()
        {
            CancelSubmoduleHistory();
            _expandedSubmodules.Clear();
            SubmoduleStatus = string.Empty;
            RebuildSubmoduleGraph();
        }

        public void EnsureSubmodulePreferences()
        {
            if (_submoduleScope != Preferences.Instance.SubmoduleHistoryScope ||
                _submoduleMode != Preferences.Instance.SubmoduleHistoryMode ||
                _submoduleLimit != Preferences.Instance.MaxExpandedSubmoduleHistories ||
                SubmoduleStatus == App.Text("SubmoduleHistory.Loading"))
                ResetSubmoduleHistories();
            else if (_submoduleLabelFormat != Preferences.Instance.SubmoduleHistoryLabelFormat)
                UpdateSubmoduleLabels();
        }

        public void UpdateSubmoduleLabels()
        {
            _submoduleLabelFormat = Preferences.Instance.SubmoduleHistoryLabelFormat;
            RebuildSubmoduleGraph();
        }

        public void ResetSubmoduleHistories()
        {
            _submoduleLabelFormat = Preferences.Instance.SubmoduleHistoryLabelFormat;
            _submoduleScope = Preferences.Instance.SubmoduleHistoryScope;
            _submoduleMode = Preferences.Instance.SubmoduleHistoryMode;
            _submoduleLimit = Preferences.Instance.MaxExpandedSubmoduleHistories;
            var selected = _selectedCommits.FirstOrDefault();
            _knownSubmodules.Clear();
            _hiddenSubmoduleOwners.Clear();
            CollapseSubmoduleHistories();
            if (selected?.SubmoduleHistory != null)
            {
                var top = selected.SubmoduleHistory;
                while (top.ParentHistory != null)
                    top = top.ParentHistory;
                selected = _rootCommits.Find(c => c.SHA == top.OwnerSHA);
            }
            if (selected != null)
            {
                var displayed = _commits.Find(c => c.GraphKey == selected.GraphKey);
                if (displayed != null)
                    SelectedCommits = [displayed];
            }
        }

        public async Task ToggleSubmoduleHistoryAsync(Models.SubmoduleHistory section)
        {
            if (!_commits.Any(c => c.GraphKey == section.HeaderKey))
                return;
            CancelSubmoduleHistory();
            if (IsSubmoduleExpanded(section))
            {
                CollapseSubmoduleHistory(section);
                return;
            }
            if (section.ParentHistory != null &&
                (Preferences.Instance.SubmoduleHistoryScope != Models.SubmoduleHistoryScope.Recursive ||
                 !IsSubmoduleExpanded(section.ParentHistory)))
                return;
            var replaced = GetSubmoduleAreasToReplace(section);
            if (_expandedSubmodules.Count(s => !replaced.Any(r => IsWithin(s, r))) >= Preferences.Instance.MaxExpandedSubmoduleHistories)
            {
                SubmoduleStatus = App.Text("SubmoduleHistory.Limit");
                RebuildSubmoduleGraph();
                return;
            }
            _submoduleRequest = new CancellationTokenSource();
            var token = _submoduleRequest.Token;
            SubmoduleStatus = string.Empty;
            try
            {
                await LoadSubmoduleAsync(section, token, replaced);
                token.ThrowIfCancellationRequested();
                RebuildSubmoduleGraph();
                if (IsSubmoduleExpanded(section))
                {
                    CancelSubmoduleHistory();
                    SelectSubmoduleHeader(section);
                }
            }
            catch (OperationCanceledException) { }
            finally { RebuildSubmoduleGraph(); }
        }

        private List<Models.SubmoduleHistory> GetSubmoduleAreasToReplace(Models.SubmoduleHistory section) =>
            Preferences.Instance.AutoCollapseSubmoduleHistories
                ? _expandedSubmodules.Where(s => s.Key != section.Key && s.ParentHistory?.Key == section.ParentHistory?.Key).ToList()
                : [];

        private async Task DiscoverSelectedSubmodulesAsync(Models.Commit commit, CancellationToken? requestToken = null)
        {
            if (!requestToken.HasValue)
                _submoduleRequest = new CancellationTokenSource();
            var token = requestToken ?? _submoduleRequest.Token;
            _expandingCommit = commit.GraphKey;
            SubmoduleStatus = App.Text("SubmoduleHistory.Loading");
            try
            {
                // Rapid keyboard navigation should not start one Git traversal per row.
                await Task.Delay(120, token);
                var changes = await new Commands.QuerySubmoduleHistory(commit.SubmoduleHistory?.RepositoryPath ?? _repo.FullPath, token).GetChangesAsync(commit);
                token.ThrowIfCancellationRequested();
                changes = changes.Select(section =>
                {
                    if (_knownSubmodules.TryGetValue(section.Key, out var known))
                        return known;
                    _knownSubmodules.Add(section.Key, section);
                    return section;
                }).ToList();
                _hiddenSubmoduleOwners.Remove(commit.GraphKey);
                SubmoduleChanges = changes;
                RebuildSubmoduleGraph();
                foreach (var section in changes)
                {
                    if (section.HeaderCommit == null)
                        await new Commands.QuerySubmoduleHistory(commit.SubmoduleHistory?.RepositoryPath ?? _repo.FullPath, token).ReadPreviewAsync(section);
                }
                token.ThrowIfCancellationRequested();
                SubmoduleStatus = string.Empty;
                if (Preferences.Instance.AutoExpandSubmoduleHistory)
                {
                    foreach (var section in changes)
                    {
                        if (IsSubmoduleExpanded(section))
                        {
                            if (Preferences.Instance.AutoCollapseSubmoduleHistories)
                                break;
                            continue;
                        }
                        var replaced = GetSubmoduleAreasToReplace(section);
                        if (_expandedSubmodules.Count(s => !replaced.Any(r => IsWithin(s, r))) >= Preferences.Instance.MaxExpandedSubmoduleHistories)
                            break;
                        await LoadSubmoduleAsync(section, token, replaced);
                        // Auto-collapse and auto-expand together open the first available changed area.
                        if (Preferences.Instance.AutoCollapseSubmoduleHistories && IsSubmoduleExpanded(section))
                            break;
                    }
                }
                token.ThrowIfCancellationRequested();
                RebuildSubmoduleGraph();
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (!token.IsCancellationRequested)
                    SubmoduleStatus = App.Text("SubmoduleHistory.QueryFailed");
            }
            finally
            {
                if (_submoduleRequest?.Token == token)
                    _expandingCommit = null;
                RebuildSubmoduleGraph();
            }
        }

        private async Task LoadSubmoduleAsync(Models.SubmoduleHistory section, CancellationToken token, List<Models.SubmoduleHistory> replaced)
        {
            section.IsLoading = true;
            RebuildSubmoduleGraph();
            try
            {
                await new Commands.QuerySubmoduleHistory(section.ParentHistory?.RepositoryPath ?? _repo.FullPath, token).LoadAsync(
                    section, Preferences.Instance.SubmoduleHistoryMode, Preferences.Instance.MaxHistoryCommits);
                token.ThrowIfCancellationRequested();
            }
            finally
            {
                section.IsLoading = false;
            }
            if (section.Error.Length > 0)
                SubmoduleStatus = $"{section.Path}: {App.Text(section.Error)}";
            else
            {
                _expandedSubmodules.RemoveAll(s => replaced.Any(r => IsWithin(s, r)));
                _expandedSubmodules.Add(section);
                if (section.IsTruncated)
                    SubmoduleStatus = App.Text("SubmoduleHistory.Truncated");
            }
        }

        public List<Models.SubmoduleHistory> GetSubmoduleComparisonChoices()
        {
            var choices = new List<Models.SubmoduleHistory>();
            var selected = _selectedCommits.FirstOrDefault()?.SubmoduleHistory;
            if (selected != null)
                choices.Add(selected);
            choices.AddRange(SubmoduleChanges.Where(c => choices.All(x => x.Key != c.Key)));
            return choices;
        }

        public async Task CompareSubmoduleAsync(Models.SubmoduleHistory section, string previousSHA)
        {
            CancelSubmoduleHistory();
            _submoduleRequest = new CancellationTokenSource();
            var token = _submoduleRequest.Token;
            var absent = App.Text("SubmoduleHistory.NoPointer");
            var detail = new SubmoduleComparisonDetail(previousSHA ?? absent)
            {
                Path = section.Path,
                Section = section,
                NewPointer = section.NewSHA ?? absent,
                PreviousPointers = section.Previous.Count == 0 ? [absent] : section.Previous.Keys.ToList(),
                Compare = value => _ = CompareSubmoduleAsync(section, value == absent ? null : value),
                Text = App.Text("SubmoduleHistory.Loading"),
            };
            DetailContext = detail;
            _ = RefreshSubmodulePointerStatusAsync(detail);
            try
            {
                var result = await new Commands.QuerySubmoduleHistory(section.ParentHistory?.RepositoryPath ?? _repo.FullPath, token)
                    .CompareAsync(section, previousSHA, Preferences.Instance.MaxHistoryCommits);
                token.ThrowIfCancellationRequested();
                detail.Text = $"{App.Text("SubmoduleHistory.AddedCommits")}\n{result.Added}\n" +
                    $"{App.Text("SubmoduleHistory.RemovedCommits")}\n{result.Removed}\n" +
                    $"{App.Text("SubmoduleHistory.FileDiff")}\n{result.Patch}";
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested)
                    detail.Text = App.Text(e.Message == "SubmoduleHistory.Unavailable" ? e.Message : "SubmoduleHistory.MissingObjects");
            }
        }

        private async Task ShowSubmoduleCommitAsync(Models.Commit commit, bool expand = true)
        {
            _submoduleRequest = new CancellationTokenSource();
            var token = _submoduleRequest.Token;
            var detail = new SubmoduleCommitDetail { Path = commit.SubmoduleHistory.Path, Section = commit.SubmoduleHistory, Text = App.Text("SubmoduleHistory.Loading") };
            DetailContext = detail;
            _ = RefreshSubmodulePointerStatusAsync(detail);
            try
            {
                var text = await new Commands.QuerySubmoduleHistory(_repo.FullPath, token)
                    .ReadCommitAsync(commit.SubmoduleHistory.RepositoryPath, commit.SHA);
                token.ThrowIfCancellationRequested();
                detail.Text = text;
                if (expand && IsSubmoduleExpanded(commit.SubmoduleHistory) && Preferences.Instance.SubmoduleHistoryScope == Models.SubmoduleHistoryScope.Recursive)
                    await DiscoverSelectedSubmodulesAsync(commit, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (!token.IsCancellationRequested)
                    detail.Text = App.Text("SubmoduleHistory.MissingObjects");
            }
        }

        public async Task RefreshSubmodulePointerStatusAsync(SubmoduleCommitDetail detail)
        {
            _pointerStatusRequest?.Cancel();
            _pointerStatusRequest?.Dispose();
            _pointerStatusRequest = new CancellationTokenSource();
            var token = _pointerStatusRequest.Token;
            detail.PointerStatus = App.Text("SubmoduleHistory.Loading");
            try
            {
                var section = detail.Section;
                var status = await new Commands.QuerySubmoduleHistory(section.ParentHistory?.RepositoryPath ?? _repo.FullPath, token)
                    .ReadPointerStatusAsync(section);
                token.ThrowIfCancellationRequested();
                detail.PointerStatus = $"{App.Text("SubmoduleHistory.RecordedPointer")}: {section.NewSHA ?? App.Text("SubmoduleHistory.NoPointer")}\n" +
                    $"{App.Text("SubmoduleHistory.LocalHEAD")}: {status.LocalHEAD ?? App.Text("SubmoduleHistory.Pointer.Unavailable")} — " +
                    App.Text($"SubmoduleHistory.Pointer.{status.State}");
            }
            catch (OperationCanceledException) { }
        }

        private void RebuildSubmoduleGraph()
        {
            foreach (var section in _knownSubmodules.Values)
            {
                section.IsExpanded = IsSubmoduleExpanded(section);
                section.ExpansionState = section.IsLoading || (section.HeaderCommit == null && section.Error.Length == 0) ? App.Text("SubmoduleHistory.Loading") :
                    section.Error.Length > 0 ? App.Text(section.Error) :
                    section.IsExpanded ? App.Text(section.IsTruncated ? "SubmoduleHistory.Truncated" : "SubmoduleHistory.Expanded") :
                    _expandedSubmodules.Count(s => !GetSubmoduleAreasToReplace(section).Any(r => IsWithin(s, r))) >= Preferences.Instance.MaxExpandedSubmoduleHistories
                        ? $"{App.Text("SubmoduleHistory.LimitShort")} · {SubmoduleExpansionCount}"
                        : App.Text("SubmoduleHistory.Collapsed");
            }
            var keys = _selectedCommits.Select(c => c.GraphKey).ToHashSet();
            var selectedSection = _selectedCommits.FirstOrDefault()?.SubmoduleHistory;
            _ignoreSelectionChange = true;
            try
            {
                _commits = Models.SubmoduleHistory.Compose(_rootCommits, _expandedSubmodules, Preferences.Instance.SubmoduleHistoryLabelFormat, _knownSubmodules.Values.Where(s => !_hiddenSubmoduleOwners.Contains(s.OwnerKey)).ToList());
                GenerateGraph(_commits);
                OnPropertyChanged(nameof(Commits));
                SelectedCommits = _commits.Where(c => keys.Contains(c.GraphKey)).ToList();
            }
            finally
            {
                _ignoreSelectionChange = false;
            }
            NotifyExpandedSubmodules();
            NotifySubmoduleNavigation();
            if (_selectedCommits.Count == 0 && selectedSection != null)
            {
                // Restore the closest visible owner without reopening collapsed descendants.
                Models.Commit owner = null;
                for (var section = selectedSection; section != null && owner == null; section = section.ParentHistory)
                    owner = _commits.Find(c => c.GraphKey == section.HeaderKey) ?? _commits.Find(c => c.GraphKey == section.OwnerKey);
                SubmoduleChanges = [];
                if (owner != null)
                {
                    _ignoreSelectionChange = true;
                    SelectedCommits = [owner];
                    _ignoreSelectionChange = false;
                    if (owner.IsSubmodulePlaceholder)
                        _ = CompareSubmoduleAsync(owner.SubmoduleHistory, owner.SubmoduleHistory.Previous.Keys.FirstOrDefault());
                    else if (owner.SubmoduleHistory == null)
                        DetailContext = new CommitDetail(_repo, _commitDetailSharedData) { Commit = owner };
                    else
                        _ = ShowSubmoduleCommitAsync(owner, false);
                }
            }
        }

        private void SelectSubmoduleHeader(Models.SubmoduleHistory section)
        {
            var row = _commits.Find(c => c.GraphKey == section.HeaderKey);
            if (row != null)
            {
                _ignoreSelectionChange = true;
                SelectedCommits = [row];
                _ignoreSelectionChange = false;
                if (row.IsSubmodulePlaceholder)
                    _ = CompareSubmoduleAsync(section, section.Previous.Keys.FirstOrDefault());
                else
                    _ = ShowSubmoduleCommitAsync(row, false);
            }
        }

        public async Task ToggleCommitSubmodulesAsync(Models.Commit commit)
        {
            if (commit.IsSubmodulePlaceholder)
            {
                await CompareSubmoduleAsync(commit.SubmoduleHistory, commit.SubmoduleHistory.Previous.Keys.FirstOrDefault());
                return;
            }
            if (commit.IsSubmoduleHeader && !IsSubmoduleExpanded(commit.SubmoduleHistory))
            {
                await ShowSubmoduleCommitAsync(commit, false);
                return;
            }
            if (commit.SubmoduleHistory != null && Preferences.Instance.SubmoduleHistoryScope != Models.SubmoduleHistoryScope.Recursive)
                return;
            var wasLoading = _expandingCommit == commit.GraphKey;
            var sections = _knownSubmodules.Values.Where(s => s.OwnerKey == commit.GraphKey).ToList();
            var hide = !_hiddenSubmoduleOwners.Contains(commit.GraphKey) && (sections.Count > 0 || wasLoading);
            CancelSubmoduleHistory();
            if (hide)
            {
                _expandedSubmodules.RemoveAll(s => sections.Any(owner => IsWithin(s, owner)));
                _hiddenSubmoduleOwners.Add(commit.GraphKey);
                SubmoduleStatus = string.Empty;
                RebuildSubmoduleGraph();
            }
            else
            {
                await DiscoverSelectedSubmodulesAsync(commit);
            }
        }

        public bool HasKnownSubmoduleChanges(Models.Commit commit) =>
            _knownSubmodules.Values.Any(s => s.OwnerKey == commit.GraphKey);

        private static bool IsWithin(Models.SubmoduleHistory candidate, Models.SubmoduleHistory ancestor)
        {
            for (var section = candidate; section != null; section = section.ParentHistory)
            {
                if (section.Key == ancestor.Key)
                    return true;
            }
            return false;
        }

        private Models.SubmoduleHistoryScope _submoduleScope;
        private Models.SubmoduleHistoryLabelFormat _submoduleLabelFormat;
        private Models.SubmoduleHistoryMode _submoduleMode;
        private int _submoduleLimit;
        private List<Models.Commit> _rootCommits = [];
        private readonly List<Models.SubmoduleHistory> _expandedSubmodules = [];
        private readonly Dictionary<string, Models.SubmoduleHistory> _knownSubmodules = [];
        private readonly HashSet<string> _hiddenSubmoduleOwners = [];
        private List<Models.SubmoduleHistory> _submoduleChanges = [];
        private CancellationTokenSource _submoduleRequest;
        private CancellationTokenSource _pointerStatusRequest;
        private string _submoduleStatus = string.Empty;
        private string _expandingCommit;
    }
}
