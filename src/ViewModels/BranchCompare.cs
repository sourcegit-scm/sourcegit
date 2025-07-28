using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class BranchCompare : ObservableObject
    {
        public string RepositoryPath
        {
            get => _repo;
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public Models.Branch Base
        {
            get => _based;
            private set => SetProperty(ref _based, value);
        }

        public Models.Branch To
        {
            get => _to;
            private set => SetProperty(ref _to, value);
        }

        public Models.Commit BaseHead
        {
            get => _baseHead;
            private set => SetProperty(ref _baseHead, value);
        }

        public Models.Commit ToHead
        {
            get => _toHead;
            private set => SetProperty(ref _toHead, value);
        }

        public List<Models.Change> VisibleChanges
        {
            get => _visibleChanges;
            private set => SetProperty(ref _visibleChanges, value);
        }

        public List<Models.Change> SelectedChanges
        {
            get => _selectedChanges;
            set
            {
                if (SetProperty(ref _selectedChanges, value))
                {
                    if (value is { Count: 1 })
                        DiffContext = new DiffContext(_repo, new Models.DiffOption(_based.Head, _to.Head, value[0]), _diffContext);
                    else
                        DiffContext = null;
                }
            }
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

        public DiffContext DiffContext
        {
            get => _diffContext;
            private set => SetProperty(ref _diffContext, value);
        }

        public BranchCompare(string repo, Models.Branch baseBranch, Models.Branch toBranch)
        {
            _repo = repo;
            _based = baseBranch;
            _to = toBranch;

            Refresh();
        }

        public void NavigateTo(string commitSHA)
        {
            var launcher = App.GetLauncher();
            if (launcher == null)
                return;

            foreach (var page in launcher.Pages)
            {
                if (page.Data is Repository repo && repo.FullPath.Equals(_repo))
                {
                    repo.NavigateToCommit(commitSHA);
                    break;
                }
            }
        }

        public void Swap()
        {
            (Base, To) = (_to, _based);

            VisibleChanges = [];
            SelectedChanges = [];

            if (_baseHead != null)
                (BaseHead, ToHead) = (_toHead, _baseHead);

            Refresh();
        }

        public void ClearSearchFilter()
        {
            SearchFilter = string.Empty;
        }

        public string GetAbsPath(string path)
        {
            return Native.OS.GetAbsPath(_repo, path);
        }

        private void Refresh()
        {
            IsLoading = true;

            Task.Run(async () =>
            {
                if (_baseHead == null)
                {
                    var baseHead = await new Commands.QuerySingleCommit(_repo, _based.Head)
                        .GetResultAsync()
                        .ConfigureAwait(false);

                    var toHead = await new Commands.QuerySingleCommit(_repo, _to.Head)
                        .GetResultAsync()
                        .ConfigureAwait(false);

                    Dispatcher.UIThread.Post(() =>
                    {
                        BaseHead = baseHead;
                        ToHead = toHead;
                    });
                }

                _changes = await new Commands.CompareRevisions(_repo, _based.Head, _to.Head)
                    .ReadAsync()
                    .ConfigureAwait(false);

                var visible = _changes;
                if (!string.IsNullOrWhiteSpace(_searchFilter))
                {
                    visible = new List<Models.Change>();
                    foreach (var c in _changes)
                    {
                        if (c.Path.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                            visible.Add(c);
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    VisibleChanges = visible;
                    IsLoading = false;

                    if (VisibleChanges.Count > 0)
                        SelectedChanges = [VisibleChanges[0]];
                    else
                        SelectedChanges = [];
                });
            });
        }

        private void RefreshVisible()
        {
            if (_changes == null)
                return;

            if (string.IsNullOrEmpty(_searchFilter))
            {
                VisibleChanges = _changes;
            }
            else
            {
                var visible = new List<Models.Change>();
                foreach (var c in _changes)
                {
                    if (c.Path.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(c);
                }

                VisibleChanges = visible;
            }
        }

        private string _repo;
        private bool _isLoading = true;
        private Models.Branch _based = null;
        private Models.Branch _to = null;
        private Models.Commit _baseHead = null;
        private Models.Commit _toHead = null;
        private List<Models.Change> _changes = null;
        private List<Models.Change> _visibleChanges = null;
        private List<Models.Change> _selectedChanges = null;
        private string _searchFilter = string.Empty;
        private DiffContext _diffContext = null;
    }
}
