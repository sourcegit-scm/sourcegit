using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Histories : ObservableObject, IDisposable
    {
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsAuthorColumnVisible
        {
            get => _repo.UIStates.IsAuthorColumnVisibleInHistory;
            set
            {
                if (_repo.UIStates.IsAuthorColumnVisibleInHistory != value)
                {
                    _repo.UIStates.IsAuthorColumnVisibleInHistory = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSHAColumnVisible
        {
            get => _repo.UIStates.IsSHAColumnVisibleInHistory;
            set
            {
                if (_repo.UIStates.IsSHAColumnVisibleInHistory != value)
                {
                    _repo.UIStates.IsSHAColumnVisibleInHistory = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsDateTimeColumnVisible
        {
            get => _repo.UIStates.IsDateTimeColumnVisibleInHistory;
            set
            {
                if (_repo.UIStates.IsDateTimeColumnVisibleInHistory != value)
                {
                    _repo.UIStates.IsDateTimeColumnVisibleInHistory = value;
                    OnPropertyChanged();
                }
            }
        }

        public List<Models.Commit> Commits
        {
            get => _commits;
            set => SetProperty(ref _commits, value);
        }

        public Models.CommitGraph Graph
        {
            get => _graph;
            set => SetProperty(ref _graph, value);
        }

        public Models.Commit SelectedCommit
        {
            get => _selectedCommit;
            set => SetProperty(ref _selectedCommit, value);
        }

        public Models.Commit LastSelectedCommit
        {
            get => _lastSelectedCommit;
            set => SetProperty(ref _lastSelectedCommit, value);
        }

        public List<Models.Commit> LastSelectedCommits
        {
            get => _lastSelectedCommits;
            private set => SetProperty(ref _lastSelectedCommits, value);
        }

        public long NavigationId
        {
            get => _navigationId;
            private set => SetProperty(ref _navigationId, value);
        }

        public IDisposable DetailContext
        {
            get => _detailContext;
            set => SetProperty(ref _detailContext, value);
        }

        public Models.Bisect Bisect
        {
            get => _bisect;
            private set => SetProperty(ref _bisect, value);
        }

        public GridLength LeftArea
        {
            get => _leftArea;
            set => SetProperty(ref _leftArea, value);
        }

        public GridLength RightArea
        {
            get => _rightArea;
            set => SetProperty(ref _rightArea, value);
        }

        public GridLength TopArea
        {
            get => _topArea;
            set => SetProperty(ref _topArea, value);
        }

        public GridLength BottomArea
        {
            get => _bottomArea;
            set => SetProperty(ref _bottomArea, value);
        }

        public Histories(Repository repo)
        {
            _repo = repo;
            _commitDetailSharedData = new CommitDetailSharedData();
        }

        public void Dispose()
        {
            Commits = [];
            _repo = null;
            _graph = null;
            _selectedCommit = null;
            _lastSelectedCommits = null;
            _lastSelectedCommit = null;
            _detailContext?.Dispose();
            _detailContext = null;
        }

        public Models.BisectState UpdateBisectInfo()
        {
            var test = Path.Combine(_repo.GitDir, "BISECT_START");
            if (!File.Exists(test))
            {
                Bisect = null;
                return Models.BisectState.None;
            }

            var info = new Models.Bisect();
            var dir = Path.Combine(_repo.GitDir, "refs", "bisect");
            if (Directory.Exists(dir))
            {
                var files = new DirectoryInfo(dir).GetFiles();
                foreach (var file in files)
                {
                    if (file.Name.StartsWith("bad"))
                        info.Bads.Add(File.ReadAllText(file.FullName).Trim());
                    else if (file.Name.StartsWith("good"))
                        info.Goods.Add(File.ReadAllText(file.FullName).Trim());
                }
            }

            Bisect = info;

            if (info.Bads.Count == 0 || info.Goods.Count == 0)
                return Models.BisectState.WaitingForRange;
            else
                return Models.BisectState.Detecting;
        }

        public void NavigateTo(string commitSHA)
        {
            var commit = _commits.Find(x => x.SHA.StartsWith(commitSHA, StringComparison.Ordinal));
            if (commit != null)
            {
                SelectedCommit = commit;
                NavigationId = _navigationId + 1;
                return;
            }

            Task.Run(async () =>
            {
                var c = await new Commands.QuerySingleCommit(_repo.FullPath, commitSHA)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    _ignoreSelectionChange = true;
                    SelectedCommit = null;

                    if (_detailContext is CommitDetail detail)
                    {
                        detail.Commit = c;
                    }
                    else
                    {
                        var commitDetail = new CommitDetail(_repo, _commitDetailSharedData);
                        commitDetail.Commit = c;
                        DetailContext = commitDetail;
                    }

                    _ignoreSelectionChange = false;
                });
            });
        }

        /// <summary>
        ///
        /// notes: if multi selects, `AutoSelectedCommit` which `Binding Mode=OneWay` will not be updated,
        /// so that we could not get the lastSelectedCommit automatically
        ///
        /// </summary>
        /// <param name="commits"></param>
        public void Select(IList commits, object selectedCommit)
        {
            if (_ignoreSelectionChange)
                return;

            if (commits.Count == 0)
            {
                _repo.SearchCommitContext.Selected = null;
                DetailContext = null;
                return;
            }
            else if (commits.Count == 1)
            {
                var commit = (commits[0] as Models.Commit)!;
                if (_repo.SearchCommitContext.Selected == null || !_repo.SearchCommitContext.Selected.SHA.Equals(commit.SHA, StringComparison.Ordinal))
                {
                    var results = _repo.SearchCommitContext.Results;
                    if (results != null)
                    {
                        _repo.SearchCommitContext.Selected = results.Find(x => x.SHA.Equals(commit.SHA, StringComparison.Ordinal));
                    }
                }

                // MarkLastSelectedCommitsAsSelected();
                SelectedCommit = commit;
                NavigationId = _navigationId + 1;

                if (_detailContext is CommitDetail detail)
                {
                    detail.Commit = commit;
                }
                else
                {
                    var commitDetail = new CommitDetail(_repo, _commitDetailSharedData);
                    commitDetail.Commit = commit;
                    DetailContext = commitDetail;
                }
            }
            else if (commits.Count == 2)
            {
                _repo.SearchCommitContext.Selected = null;

                var end = commits[0] as Models.Commit;
                var start = commits[1] as Models.Commit;
                DetailContext = new RevisionCompare(_repo, start, end);
            }
            else
            {
                _repo.SearchCommitContext.Selected = null;
                DetailContext = new Models.Count(commits.Count);
            }

            LastSelectedCommit = selectedCommit as Models.Commit;

            var lastSelected = new List<Models.Commit>();
            foreach (var obj in commits)
            {
                if (obj is Models.Commit c)
                    lastSelected.Add(c);
            }
            LastSelectedCommits = lastSelected;
        }

        public void MarkLastSelectedCommitsAsSelected(IList<Models.Commit> commits)
        {
            if (commits == null || commits.Count == 0)
            {
                LastSelectedCommits = [];
                NavigationId = _navigationId + 1;
                return;
            }

            var selectedSHAs = new HashSet<string>();
            foreach (var c in commits)
                selectedSHAs.Add(c.SHA);

            var availableCommits = new List<Models.Commit>();
            foreach (var c in Commits)
            {
                if (selectedSHAs.Contains(c.SHA))
                    availableCommits.Add(c);
            }

            // if LastSelectedCommits is not empty, find the AutoSelectedCommit or get last one
            if (availableCommits.Count > 0 && LastSelectedCommit != null)
            {
                // List.Find is not LINQ, it's a method of List<T>
                SelectedCommit = availableCommits.Find(x => x.SHA == LastSelectedCommit.SHA)
                                  ?? availableCommits[0];
            }

            // set selectItem above(1item), now select others too
            LastSelectedCommits = availableCommits;
            NavigationId = _navigationId + 1;
        }

        public async Task<Models.Commit> GetCommitAsync(string sha)
        {
            return await new Commands.QuerySingleCommit(_repo.FullPath, sha)
                .GetResultAsync()
                .ConfigureAwait(false);
        }

        public async Task<bool> CheckoutBranchByDecoratorAsync(Models.Decorator decorator)
        {
            if (decorator == null)
                return false;

            if (decorator.Type == Models.DecoratorType.CurrentBranchHead ||
                decorator.Type == Models.DecoratorType.CurrentCommitHead)
                return true;

            if (decorator.Type == Models.DecoratorType.LocalBranchHead)
            {
                var b = _repo.Branches.Find(x => x.Name == decorator.Name);
                if (b == null)
                    return false;

                await _repo.CheckoutBranchAsync(b);
                return true;
            }

            if (decorator.Type == Models.DecoratorType.RemoteBranchHead)
            {
                var rb = _repo.Branches.Find(x => x.FriendlyName == decorator.Name);
                if (rb == null)
                    return false;

                var lb = _repo.Branches.Find(x => x.IsLocal && x.Upstream == rb.FullName);
                if (lb == null || lb.Ahead.Count > 0)
                {
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new CreateBranch(_repo, rb));
                }
                else if (lb.Behind.Count > 0)
                {
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new CheckoutAndFastForward(_repo, lb, rb));
                }
                else if (!lb.IsCurrent)
                {
                    await _repo.CheckoutBranchAsync(lb);
                }

                return true;
            }

            return false;
        }

        public async Task CheckoutBranchByCommitAsync(Models.Commit commit)
        {
            if (commit.IsCurrentHead)
                return;

            Models.Branch firstRemoteBranch = null;
            foreach (var d in commit.Decorators)
            {
                if (d.Type == Models.DecoratorType.LocalBranchHead)
                {
                    var b = _repo.Branches.Find(x => x.Name == d.Name);
                    if (b == null)
                        continue;

                    await _repo.CheckoutBranchAsync(b);
                    return;
                }

                if (d.Type == Models.DecoratorType.RemoteBranchHead)
                {
                    var rb = _repo.Branches.Find(x => x.FriendlyName == d.Name);
                    if (rb == null)
                        continue;

                    var lb = _repo.Branches.Find(x => x.IsLocal && x.Upstream == rb.FullName);
                    if (lb != null && lb.Behind.Count > 0 && lb.Ahead.Count == 0)
                    {
                        if (_repo.CanCreatePopup())
                            _repo.ShowPopup(new CheckoutAndFastForward(_repo, lb, rb));
                        return;
                    }

                    firstRemoteBranch ??= rb;
                }
            }

            if (_repo.CanCreatePopup())
            {
                if (firstRemoteBranch != null)
                    _repo.ShowPopup(new CreateBranch(_repo, firstRemoteBranch));
                else if (!_repo.IsBare)
                    _repo.ShowPopup(new CheckoutCommit(_repo, commit));
            }
        }

        public async Task CherryPickAsync(Models.Commit commit)
        {
            if (_repo.CanCreatePopup())
            {
                if (commit.Parents.Count <= 1)
                {
                    _repo.ShowPopup(new CherryPick(_repo, [commit]));
                }
                else
                {
                    var parents = new List<Models.Commit>();
                    foreach (var sha in commit.Parents)
                    {
                        var parent = _commits.Find(x => x.SHA.Equals(sha, StringComparison.Ordinal));
                        if (parent == null)
                            parent = await new Commands.QuerySingleCommit(_repo.FullPath, sha).GetResultAsync();

                        if (parent != null)
                            parents.Add(parent);
                    }

                    _repo.ShowPopup(new CherryPick(_repo, commit, parents));
                }
            }
        }

        public async Task RewordHeadAsync(Models.Commit head)
        {
            if (_repo.CanCreatePopup())
            {
                var message = await new Commands.QueryCommitFullMessage(_repo.FullPath, head.SHA).GetResultAsync();
                _repo.ShowPopup(new Reword(_repo, head, message));
            }
        }

        public async Task SquashOrFixupHeadAsync(Models.Commit head, bool fixup)
        {
            if (head.Parents.Count == 1)
            {
                var parent = await new Commands.QuerySingleCommit(_repo.FullPath, head.Parents[0]).GetResultAsync();
                if (parent == null)
                    return;

                string message = await new Commands.QueryCommitFullMessage(_repo.FullPath, head.Parents[0]).GetResultAsync();
                if (!fixup)
                {
                    var headMessage = await new Commands.QueryCommitFullMessage(_repo.FullPath, head.SHA).GetResultAsync();
                    message = $"{message}\n\n{headMessage}";
                }

                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new SquashOrFixupHead(_repo, parent, message, fixup));
            }
        }

        public async Task DropHeadAsync(Models.Commit head)
        {
            var parent = _commits.Find(x => x.SHA.Equals(head.Parents[0]));
            if (parent == null)
                parent = await new Commands.QuerySingleCommit(_repo.FullPath, head.Parents[0]).GetResultAsync();

            if (parent != null && _repo.CanCreatePopup())
                _repo.ShowPopup(new DropHead(_repo, head, parent));
        }

        public async Task<string> GetCommitFullMessageAsync(Models.Commit commit)
        {
            return await new Commands.QueryCommitFullMessage(_repo.FullPath, commit.SHA)
                .GetResultAsync()
                .ConfigureAwait(false);
        }

        public async Task<Models.Commit> CompareWithHeadAsync(Models.Commit commit)
        {
            var head = _commits.Find(x => x.IsCurrentHead);
            if (head == null)
            {
                _repo.SearchCommitContext.Selected = null;
                head = await new Commands.QuerySingleCommit(_repo.FullPath, "HEAD").GetResultAsync();
                if (head != null)
                    DetailContext = new RevisionCompare(_repo, commit, head);

                return null;
            }

            return head;
        }

        public void CompareWithWorktree(Models.Commit commit)
        {
            DetailContext = new RevisionCompare(_repo, commit, null);
        }

        private Repository _repo = null;
        private CommitDetailSharedData _commitDetailSharedData = null;
        private bool _isLoading = true;
        private List<Models.Commit> _commits = new List<Models.Commit>();
        private List<Models.Commit> _lastSelectedCommits = [];
        private Models.Commit _lastSelectedCommit = null;
        private Models.CommitGraph _graph = null;
        private Models.Commit _selectedCommit = null;
        private Models.Bisect _bisect = null;
        private long _navigationId = 0;
        private IDisposable _detailContext = null;
        private bool _ignoreSelectionChange = false;

        private GridLength _leftArea = new GridLength(1, GridUnitType.Star);
        private GridLength _rightArea = new GridLength(1, GridUnitType.Star);
        private GridLength _topArea = new GridLength(1, GridUnitType.Star);
        private GridLength _bottomArea = new GridLength(1, GridUnitType.Star);
    }
}
