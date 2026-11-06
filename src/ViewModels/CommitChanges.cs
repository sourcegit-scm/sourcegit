using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class CommitChanges : ObservableObject, IDisposable
    {
        public int CommitCount => _commits.Count;
        public IReadOnlyList<Models.Commit> Commits { get; }
        public IReadOnlyList<CommitDetail> InformationDetails { get; }

        public CommitDetail SelectedInformationDetail
        {
            get => _selectedInformationDetail;
            set
            {
                if (value != null && InformationDetails.Contains(value))
                    SelectedCommit = value.Commit;
            }
        }

        public int ActiveTabIndex
        {
            get => _activeTabIndex;
            set
            {
                if (!_disposed && value is >= 0 and <= 2 && SetProperty(ref _activeTabIndex, value) && value == 2)
                    EnsureRevisionDetail();
            }
        }

        public Models.Commit SelectedCommit
        {
            get => _selectedCommit;
            set
            {
                if (_disposed || value == null || !_commits.Contains(value))
                    return;
                if (SetProperty(ref _selectedCommit, value))
                {
                    _selectedInformationDetail = InformationDetails.First(detail => detail.Commit == value);
                    OnPropertyChanged(nameof(SelectedInformationDetail));
                    if (_revisionDetail != null)
                    {
                        _revisionDetail.Dispose();
                        RevisionDetail = null;
                        EnsureRevisionDetail();
                    }
                }
            }
        }

        public CommitDetail RevisionDetail
        {
            get => _revisionDetail;
            private set => SetProperty(ref _revisionDetail, value);
        }

        private void EnsureRevisionDetail()
        {
            if (_revisionDetail == null && _selectedCommit != null && !_disposed)
                RevisionDetail = new CommitDetail(_repo, null) { ActiveTabIndex = 2, Commit = _selectedCommit };
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }
        public string Error
        {
            get => _error;
            private set => SetProperty(ref _error, value);
        }
        public int TotalChanges => _changes.Count;
        public List<Models.Change> VisibleChanges
        {
            get => _visibleChanges;
            private set => SetProperty(ref _visibleChanges, value);
        }
        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                    RefreshVisible();
            }
        }
        public ChangeSelection ChangeSelection
        {
            get => _changeSelection;
            set
            {
                if (SetProperty(ref _changeSelection, value))
                {
                    DiffContext = value is { Count: 1, HasFolder: false } && value.Changes[0] is Models.CommitChange change
                        ? new DiffContext(_repo.FullPath, new Models.DiffOption(change))
                        : null;
                }
            }
        }
        public DiffContext DiffContext
        {
            get => _diffContext;
            private set => SetProperty(ref _diffContext, value);
        }

        public CommitChanges(Repository repo, List<Models.Commit> commits)
        {
            _repo = repo;
            _commits = new List<Models.Commit>(commits);
            Commits = _commits.AsReadOnly();
            // Virtualized card views start these queries only when realized. A shared
            // gate also bounds git processes when scrolling quickly through many cards.
            var gate = new SemaphoreSlim(2);
            InformationDetails = _commits.Select(commit => new CommitDetail(repo,
                new CommitDetailSharedData { ActiveTabIndex = 0 }, true, gate)
            { Commit = commit }).ToList().AsReadOnly();
            _selectedCommit = _commits.Count > 0 ? _commits[0] : null;
            _selectedInformationDetail = InformationDetails.FirstOrDefault();
            _ = LoadAsync(_cancellation.Token);
        }

        public void ClearSearchFilter() => SearchFilter = string.Empty;

        public void OpenChangeWithExternalDiffTool(Models.Change change)
        {
            if (change is Models.CommitChange selected && selected.BeforeMode != "160000" && selected.AfterMode != "160000")
                new Commands.DiffTool(_repo.FullPath, new Models.DiffOption(selected)).Open();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _revisionDetail?.Dispose();
            foreach (var detail in InformationDetails)
                detail.Dispose();
            _cancellation.Cancel();
            _cancellation.Dispose();
        }

        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            try
            {
                var changes = await new Commands.QuerySelectedCommitChanges(_repo.FullPath)
                    .ReadAsync(_commits, cancellationToken).ConfigureAwait(false);
                changes.Sort((l, r) => Models.NumericSort.Compare(l.Path, r.Path));
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_disposed || cancellationToken.IsCancellationRequested)
                        return;
                    _changes = new List<Models.Change>(changes);
                    OnPropertyChanged(nameof(TotalChanges));
                    IsLoading = false;
                    RefreshVisible();
                });
            }
            catch (OperationCanceledException)
            {
                // A later history selection owns the details pane now.
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_disposed || cancellationToken.IsCancellationRequested)
                        return;
                    Error = ex.Message;
                    IsLoading = false;
                });
            }
        }

        private void RefreshVisible()
        {
            var visible = new List<Models.Change>();
            foreach (var change in _changes)
            {
                if (string.IsNullOrWhiteSpace(_searchFilter) ||
                    change.Path.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                    change.OriginalPath.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                    visible.Add(change);
            }
            VisibleChanges = visible;
            var previous = _changeSelection is { Count: 1, HasFolder: false } ? _changeSelection.Changes[0] : null;
            ChangeSelection = previous != null && visible.Contains(previous)
                ? new ChangeSelection(new List<Models.Change> { previous })
                : new ChangeSelection(visible.Count > 0 ? visible.GetRange(0, 1) : null);
        }

        private readonly Repository _repo;
        private readonly List<Models.Commit> _commits;
        private readonly CancellationTokenSource _cancellation = new();
        private int _activeTabIndex = 1;
        private Models.Commit _selectedCommit;
        private CommitDetail _revisionDetail;
        private CommitDetail _selectedInformationDetail;
        private bool _disposed;
        private bool _isLoading = true;
        private string _error = string.Empty;
        private string _searchFilter = string.Empty;
        private List<Models.Change> _changes = [];
        private List<Models.Change> _visibleChanges = [];
        private ChangeSelection _changeSelection = new(null);
        private DiffContext _diffContext;
    }
}
