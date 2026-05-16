using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Histories : ObservableObject
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
            set
            {
                if (SetProperty(ref _commits, value))
                    PostCommitsChanged();
            }
        }

        public Models.CommitGraph Graph
        {
            get => _graph;
            set => SetProperty(ref _graph, value);
        }

        public long HoveredCommitIndex
        {
            get => _hoveredCommitIndex;
            set
            {
                if (SetProperty(ref _hoveredCommitIndex, value))
                {
                    if (Preferences.Instance.EnableHoverViewTracking && value >= 0 && value < _commits.Count)
                    {
                        var hoveredIndex = (int)value;
                        var depth = 1000u;
                        var topLimit = -1;
                        var bottomLimit = -1;

                        if (_visibleTopIndex >= 0 && _visibleBottomIndex >= _visibleTopIndex)
                        {
                            topLimit = _visibleTopIndex;
                            bottomLimit = _visibleBottomIndex;

                            // Add a small guard band to keep context around viewport edges.
                            var dist = Math.Max(Math.Abs(hoveredIndex - topLimit), Math.Abs(bottomLimit - hoveredIndex));
                            depth = (uint)Math.Max(500, dist + 32);
                        }

                        HoveredLineageCommits = GetCommitLineage(
                            _commits[hoveredIndex],
                            Models.CommitLineageSearchMethod.FullLineage,
                            depth,
                            topLimit,
                            bottomLimit);
                    }
                    else
                        HoveredLineageCommits = null;
                }
            }
        }

        public bool[] HoveredLineageCommits
        {
            get => _hoveredLineageCommits;
            set => SetProperty(ref _hoveredLineageCommits, value);
        }

        public List<Models.Commit> SelectedCommits
        {
            get => _selectedCommits;
            set
            {
                if (SetProperty(ref _selectedCommits, value))
                    PostSelectedCommitsChanged();
            }
        }

        public HashSet<int> SelectedLineagePaths
        {
            get => _selectedLineagePaths;
            set => SetProperty(ref _selectedLineagePaths, value);
        }

        public bool[] SelectedLineageCommits
        {
            get => _selectedLineageCommits;
            set => SetProperty(ref _selectedLineageCommits, value);
        }

        public object DetailContext
        {
            get => _detailContext;
            set
            {
                if (SetProperty(ref _detailContext, value))
                    OnPropertyChanged(nameof(IsOpenAsStandaloneVisible));
            }
        }

        public Models.Bisect Bisect
        {
            get => _bisect;
            private set => SetProperty(ref _bisect, value);
        }

        public Models.Branch CurrentBranch
        {
            get => _repo.CurrentBranch;
        }

        public Models.CommitGraphHighlighting CommitGraphHighlighting
        {
            get => _repo.UIStates.CommitGraphHighlighting;
            set
            {
                if (_repo.UIStates.CommitGraphHighlighting != value)
                {
                    _repo.UIStates.CommitGraphHighlighting = value;
                    OnPropertyChanged();

                    var highlightSelected = value == Models.CommitGraphHighlighting.SelectedLineageOnly ||
                                             value == Models.CommitGraphHighlighting.CurrentBranchAndSelectedLineage;
                    if (highlightSelected && _selectedCommits.Count == 1)
                        CalculateTargetLineage(_selectedCommits[0]);
                    else if (!highlightSelected)
                    {
                        SelectedLineageCommits = null;
                        SelectedLineagePaths = null;
                    }
                }
            }
        }

        public Models.CommitLineageSearchMethod LineageSearchMethod
        {
            get => _repo.UIStates.LineageSearchMethod;
            set
            {
                if (_repo.UIStates.LineageSearchMethod != value)
                {
                    _repo.UIStates.LineageSearchMethod = value;
                    OnPropertyChanged();

                    if (_selectedCommits.Count == 1 &&
                        (CommitGraphHighlighting == Models.CommitGraphHighlighting.SelectedLineageOnly ||
                         CommitGraphHighlighting == Models.CommitGraphHighlighting.CurrentBranchAndSelectedLineage))
                    {
                        CalculateTargetLineage(_selectedCommits[0]);
                    }
                }
            }
        }

        public AvaloniaList<Models.IssueTracker> IssueTrackers
        {
            get => _repo.IssueTrackers;
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
            get => _isCollapseDetails ? new GridLength(28, GridUnitType.Pixel) : _bottomArea;
            set
            {
                if (!Preferences.Instance.UseTwoColumnsLayoutInHistories && !_isCollapseDetails)
                    SetProperty(ref _bottomArea, value);
            }
        }

        public bool IsOpenAsStandaloneVisible
        {
            get => DetailContext is CommitDetail or RevisionCompare;
        }

        public bool IsCollapseDetails
        {
            get => _isCollapseDetails;
            set
            {
                if (!Preferences.Instance.UseTwoColumnsLayoutInHistories && SetProperty(ref _isCollapseDetails, value))
                {
                    OnPropertyChanged(nameof(TopArea));
                    OnPropertyChanged(nameof(BottomArea));
                }
            }
        }

        public Histories(Repository repo)
        {
            _repo = repo;
            _commitDetailSharedData = new CommitDetailSharedData();
        }

        public void NotifyCurrentBranchChanged()
        {
            OnPropertyChanged(nameof(CurrentBranch));
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
                SelectedCommits = [commit];
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
                    SelectedCommits = [];

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

        public async Task<Models.Commit> GetCommitAsync(string sha)
        {
            return await new Commands.QuerySingleCommit(_repo.FullPath, sha)
                .GetResultAsync()
                .ConfigureAwait(false);
        }

        public void SetVisibleCommitRange(int topIndex, int bottomIndex)
        {
            // 扩展 viewport 上下各 100 行，保证高亮连续性
            const int VIEWPORT_PADDING = 100;
            int paddedTop = Math.Max(0, topIndex - VIEWPORT_PADDING);
            int paddedBottom = Math.Min(_commits.Count - 1, bottomIndex + VIEWPORT_PADDING);

            // 只有当滚动超过阈值时才触发 lineage 重新计算
            const int SCROLL_THRESHOLD = 40; // 可根据实际体验调整
            bool needUpdate = false;
            if (_visibleTopIndex < 0 || _visibleBottomIndex < 0)
            {
                needUpdate = true;
            }
            else if (Math.Abs(paddedTop - _visibleTopIndex) > SCROLL_THRESHOLD ||
                     Math.Abs(paddedBottom - _visibleBottomIndex) > SCROLL_THRESHOLD)
            {
                needUpdate = true;
            }

            if (needUpdate)
            {
                _visibleTopIndex = paddedTop;
                _visibleBottomIndex = paddedBottom;
                // 触发 hover lineage 重新计算（如果有 hover）
                if (_hoveredCommitIndex >= 0 && _hoveredCommitIndex < _commits.Count)
                {
                    // 重新赋值以触发 setter
                    var tmp = _hoveredCommitIndex;
                    _hoveredCommitIndex = -1;
                    HoveredCommitIndex = tmp;
                }
            }
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

        private void PostCommitsChanged()
        {
            _commitMap.Clear();
            for (int i = 0; i < _commits.Count; i++)
            {
                var c = _commits[i];
                c.Index = i;
                _commitMap[c.SHA] = c;
            }

            if (_selectedCommits.Count == 0)
                return;

            if (_commits.Count == 0 || _selectedCommits.Count > 20)
            {
                SelectedCommits = [];
                return;
            }

            var set = new HashSet<string>();
            foreach (var c in _selectedCommits)
                set.Add(c.SHA);

            var selected = new List<Models.Commit>();
            foreach (var c in _commits)
            {
                if (set.Contains(c.SHA))
                {
                    selected.Add(c);
                    set.Remove(c.SHA);
                    if (set.Count == 0)
                        break;
                }
            }

            SelectedCommits = selected;
        }

        private void CalculateTargetLineage(Models.Commit commit)
        {
            Task.Run(() =>
            {
                if (commit == null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        SelectedLineageCommits = null;
                        SelectedLineagePaths = null;
                    });
                    return;
                }

                var paths = new HashSet<int>();
                var lineage = GetCommitLineage(commit, LineageSearchMethod, 20000);
                for (int i = 0; i < lineage.Length; i++)
                {
                    if (lineage[i])
                    {
                        var c = _commits[i];
                        if (c.PathIndex >= 0)
                            paths.Add(c.PathIndex);
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    SelectedLineageCommits = lineage;
                    SelectedLineagePaths = paths;
                });
            });
        }

        private void PostSelectedCommitsChanged()
        {
            if (_ignoreSelectionChange)
                return;

            if (_selectedCommits.Count == 0)
            {
                _repo.SearchCommitContext.Selected = null;
                DetailContext = new Models.Null();
                SelectedLineageCommits = null;
                SelectedLineagePaths = null;
            }
            else if (_selectedCommits.Count == 1)
            {
                var c = _selectedCommits[0];
                if (_repo.SearchCommitContext.Selected == null || !_repo.SearchCommitContext.Selected.SHA.Equals(c.SHA, StringComparison.Ordinal))
                    _repo.SearchCommitContext.Selected = _repo.SearchCommitContext.Results?.Find(x => x.SHA.Equals(c.SHA, StringComparison.Ordinal));

                if (_detailContext is CommitDetail detail)
                    detail.Commit = c;
                else
                    DetailContext = new CommitDetail(_repo, _commitDetailSharedData) { Commit = c };

                var highlightSelected = _repo.UIStates.CommitGraphHighlighting == Models.CommitGraphHighlighting.SelectedLineageOnly ||
                                         _repo.UIStates.CommitGraphHighlighting == Models.CommitGraphHighlighting.CurrentBranchAndSelectedLineage;
                if (highlightSelected)
                    CalculateTargetLineage(c);
            }
            else if (_selectedCommits.Count == 2)
            {
                _repo.SearchCommitContext.Selected = null;

                if (_detailContext is RevisionCompare compare)
                    compare.SetTargets(_selectedCommits[1], _selectedCommits[0]);
                else
                    DetailContext = new RevisionCompare(_repo, _selectedCommits[1], _selectedCommits[0]);

                SelectedLineageCommits = null;
                SelectedLineagePaths = null;
            }
            else
            {
                _repo.SearchCommitContext.Selected = null;
                DetailContext = new Models.Count(_selectedCommits.Count);
                SelectedLineageCommits = null;
                SelectedLineagePaths = null;
            }
        }

        public bool[] GetCommitLineage(
            Models.Commit commit,
            Models.CommitLineageSearchMethod method,
            uint depth = 100,
            int viewportTopIndex = -1,
            int viewportBottomIndex = -1)
        {
            var active = new bool[_commits.Count];
            if (commit == null || method == Models.CommitLineageSearchMethod.None)
                return active;

            active[commit.Index] = true;

            // GUI boundaries: only search within a limited range of commits to improve performance
            int topLimit = Math.Max(0, commit.Index - (int)depth);
            int bottomLimit = Math.Min(_commits.Count - 1, commit.Index + (int)depth);

            // Viewport boundaries: tighten the traversal window to visible rows when available.
            if (viewportTopIndex >= 0 && viewportBottomIndex >= viewportTopIndex)
            {
                topLimit = Math.Max(topLimit, viewportTopIndex);
                bottomLimit = Math.Min(bottomLimit, viewportBottomIndex);
            }

            // DOWN (Descendants) - Moving to lower indices
            if (method == Models.CommitLineageSearchMethod.ChildsOnly || method == Models.CommitLineageSearchMethod.FullLineage)
            {
                for (int i = commit.Index - 1; i >= topLimit; i--)
                {
                    foreach (var pSha in _commits[i].Parents)
                    {
                        if (_commitMap.TryGetValue(pSha, out var parent) && parent.Index < _commits.Count && active[parent.Index])
                        {
                            active[i] = true;
                            break;
                        }
                    }
                }
            }

            // UP (Ancestors) - Moving to higher indices
            if (method == Models.CommitLineageSearchMethod.ParentsOnly || method == Models.CommitLineageSearchMethod.FullLineage)
            {
                for (int i = commit.Index; i <= bottomLimit; i++)
                {
                    if (active[i])
                    {
                        foreach (var pSha in _commits[i].Parents)
                        {
                            if (_commitMap.TryGetValue(pSha, out var parent) && parent.Index <= bottomLimit)
                            {
                                active[parent.Index] = true;
                            }
                        }
                    }
                }
            }

            return active;
        }

        private Repository _repo = null;
        private CommitDetailSharedData _commitDetailSharedData = null;
        private bool _isLoading = true;
        private List<Models.Commit> _commits = new List<Models.Commit>();
        private Models.CommitGraph _graph = null;
        private long _hoveredCommitIndex = -1;
        private bool[] _hoveredLineageCommits = null;
        private List<Models.Commit> _selectedCommits = [];
        private Models.Bisect _bisect = null;
        private object _detailContext = new Models.Null();
        private bool _ignoreSelectionChange = false;

        private GridLength _leftArea = new GridLength(1, GridUnitType.Star);
        private GridLength _rightArea = new GridLength(1, GridUnitType.Star);
        private GridLength _topArea = new GridLength(1, GridUnitType.Star);
        private GridLength _bottomArea = new GridLength(1, GridUnitType.Star);
        private bool _isCollapseDetails = false;
        private HashSet<int> _selectedLineagePaths = null;
        private bool[] _selectedLineageCommits = null;
        private int _visibleTopIndex = -1;
        private int _visibleBottomIndex = -1;
        private Dictionary<string, Models.Commit> _commitMap = new Dictionary<string, Models.Commit>();
    }
}
