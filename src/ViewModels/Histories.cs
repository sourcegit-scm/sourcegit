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

        public List<Models.Commit> Commits
        {
            get => _commits;
            set
            {
                var lastSelected = AutoSelectedCommit;
                if (SetProperty(ref _commits, value))
                {
                    if (value.Count > 0 && lastSelected != null)
                        AutoSelectedCommit = value.Find(x => x.SHA == lastSelected.SHA);
                }
            }
        }

        public Models.CommitGraph Graph
        {
            get => _graph;
            set => SetProperty(ref _graph, value);
        }

        public Models.Commit AutoSelectedCommit
        {
            get => _autoSelectedCommit;
            set => SetProperty(ref _autoSelectedCommit, value);
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
        }

        public void Dispose()
        {
            Commits = [];
            _repo = null;
            _graph = null;
            _autoSelectedCommit = null;
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
                NavigateTo(commit);
                return;
            }

            Task.Run(async () =>
            {
                var c = await new Commands.QuerySingleCommit(_repo.FullPath, commitSHA)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() => NavigateTo(c));
            });
        }

        public void Select(IList commits)
        {
            if (commits.Count == 0)
            {
                _repo.SelectedSearchedCommit = null;
                DetailContext = null;
            }
            else if (commits.Count == 1)
            {
                var commit = (commits[0] as Models.Commit)!;
                if (_repo.SelectedSearchedCommit == null || _repo.SelectedSearchedCommit.SHA != commit.SHA)
                    _repo.SelectedSearchedCommit = _repo.SearchedCommits.Find(x => x.SHA == commit.SHA);

                AutoSelectedCommit = commit;
                NavigationId = _navigationId + 1;

                if (_detailContext is CommitDetail detail)
                {
                    detail.Commit = commit;
                }
                else
                {
                    var commitDetail = new CommitDetail(_repo, true);
                    commitDetail.Commit = commit;
                    DetailContext = commitDetail;
                }
            }
            else if (commits.Count == 2)
            {
                _repo.SelectedSearchedCommit = null;

                var end = commits[0] as Models.Commit;
                var start = commits[1] as Models.Commit;
                DetailContext = new RevisionCompare(_repo.FullPath, start, end);
            }
            else
            {
                _repo.SelectedSearchedCommit = null;
                DetailContext = new Models.Count(commits.Count);
            }
        }

        public bool CheckoutBranchByDecorator(Models.Decorator decorator)
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

                _repo.CheckoutBranch(b);
                return true;
            }

            if (decorator.Type == Models.DecoratorType.RemoteBranchHead)
            {
                var rb = _repo.Branches.Find(x => x.FriendlyName == decorator.Name);
                if (rb == null)
                    return false;

                var lb = _repo.Branches.Find(x => x.IsLocal && x.Upstream == rb.FullName);
                if (lb == null || lb.TrackStatus.Ahead.Count > 0)
                {
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new CreateBranch(_repo, rb));
                }
                else if (lb.TrackStatus.Behind.Count > 0)
                {
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new CheckoutAndFastForward(_repo, lb, rb));
                }
                else if (!lb.IsCurrent)
                {
                    _repo.CheckoutBranch(lb);
                }

                return true;
            }

            return false;
        }

        public void CheckoutBranchByCommit(Models.Commit commit)
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

                    _repo.CheckoutBranch(b);
                    return;
                }

                if (d.Type == Models.DecoratorType.RemoteBranchHead)
                {
                    var rb = _repo.Branches.Find(x => x.FriendlyName == d.Name);
                    if (rb == null)
                        continue;

                    var lb = _repo.Branches.Find(x => x.IsLocal && x.Upstream == rb.FullName);
                    if (lb is { TrackStatus.Ahead.Count: 0 })
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

        private void NavigateTo(Models.Commit commit)
        {
            AutoSelectedCommit = commit;

            if (commit == null)
            {
                DetailContext = null;
            }
            else
            {
                NavigationId = _navigationId + 1;

                if (_detailContext is CommitDetail detail)
                {
                    detail.Commit = commit;
                }
                else
                {
                    var commitDetail = new CommitDetail(_repo, true);
                    commitDetail.Commit = commit;
                    DetailContext = commitDetail;
                }
            }
        }

        private Repository _repo = null;
        private bool _isLoading = true;
        private List<Models.Commit> _commits = new List<Models.Commit>();
        private Models.CommitGraph _graph = null;
        private Models.Commit _autoSelectedCommit = null;
        private Models.Bisect _bisect = null;
        private long _navigationId = 0;
        private IDisposable _detailContext = null;

        private GridLength _leftArea = new GridLength(1, GridUnitType.Star);
        private GridLength _rightArea = new GridLength(1, GridUnitType.Star);
        private GridLength _topArea = new GridLength(1, GridUnitType.Star);
        private GridLength _bottomArea = new GridLength(1, GridUnitType.Star);
    }
}
