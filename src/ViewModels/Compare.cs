using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Compare : ObservableObject
    {
        public bool IsLoadingChanges
        {
            get => _isLoadingChanges;
            private set => SetProperty(ref _isLoadingChanges, value);
        }

        public bool IsLoadingPickableCommits
        {
            get => _isLoadingPickableCommits;
            private set => SetProperty(ref _isLoadingPickableCommits, value);
        }

        public bool IsViewChanges
        {
            get => _isViewChanges;
            set => SetProperty(ref _isViewChanges, value);
        }

        public bool CanResetFiles
        {
            get => _canResetFiles;
        }

        public string BaseName
        {
            get => _baseName;
            private set => SetProperty(ref _baseName, value);
        }

        public string ToName
        {
            get => _toName;
            private set => SetProperty(ref _toName, value);
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

        public int TotalChanges
        {
            get => _totalChanges;
            private set => SetProperty(ref _totalChanges, value);
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
                        DiffContext = new DiffContext(_repo.FullPath, new Models.DiffOption(_based, _to, value[0]), _diffContext);
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
                    RefreshVisibleChanges();
            }
        }

        public DiffContext DiffContext
        {
            get => _diffContext;
            private set => SetProperty(ref _diffContext, value);
        }

        public List<Models.Commit> LeftOnlyCommits
        {
            get => _leftOnlyCommits;
            private set => SetProperty(ref _leftOnlyCommits, value);
        }

        public List<Models.Commit> RightOnlyCommits
        {
            get => _rightOnlyCommits;
            private set => SetProperty(ref _rightOnlyCommits, value);
        }

        public Compare(Repository repo, object based, object to)
        {
            _repo = repo;
            _canResetFiles = !repo.IsBare;
            _based = GetSHA(based);
            _to = GetSHA(to);
            _baseName = GetName(based);
            _toName = GetName(to);

            _baseHead = new Commands.QuerySingleCommit(_repo.FullPath, _based).GetResult();
            _toHead = new Commands.QuerySingleCommit(_repo.FullPath, _to).GetResult();

            UpdatePickableCommits();
            UpdateChanges();
        }

        public void NavigateTo(string commitSHA)
        {
            _repo.NavigateToCommit(commitSHA);
        }

        public void Swap()
        {
            (_based, _to) = (_to, _based);
            (BaseName, ToName) = (_toName, _baseName);
            (BaseHead, ToHead) = (_toHead, _baseHead);
            (LeftOnlyCommits, RightOnlyCommits) = (_rightOnlyCommits, _leftOnlyCommits);
            UpdateChanges();
        }

        public void ClearSearchFilter()
        {
            SearchFilter = string.Empty;
        }

        public string GetAbsPath(string path)
        {
            return Native.OS.GetAbsPath(_repo.FullPath, path);
        }

        public void OpenInExternalDiffTool(Models.Change change)
        {
            new Commands.DiffTool(_repo.FullPath, new Models.DiffOption(_based, _to, change)).Open();
        }

        public async Task ResetToLeftAsync(Models.Change change)
        {
            if (!_canResetFiles)
                return;

            if (change.Index == Models.ChangeState.Added)
            {
                var fullpath = Native.OS.GetAbsPath(_repo.FullPath, change.Path);
                if (File.Exists(fullpath))
                    await new Commands.Remove(_repo.FullPath, [change.Path]).ExecAsync();
            }
            else if (change.Index == Models.ChangeState.Renamed)
            {
                var renamed = Native.OS.GetAbsPath(_repo.FullPath, change.Path);
                if (File.Exists(renamed))
                    await new Commands.Remove(_repo.FullPath, [change.Path]).ExecAsync();

                await new Commands.Checkout(_repo.FullPath).FileWithRevisionAsync(change.OriginalPath, _baseHead.SHA);
            }
            else
            {
                await new Commands.Checkout(_repo.FullPath).FileWithRevisionAsync(change.Path, _baseHead.SHA);
            }
        }

        public async Task ResetToRightAsync(Models.Change change)
        {
            if (change.Index == Models.ChangeState.Deleted)
            {
                var fullpath = Native.OS.GetAbsPath(_repo.FullPath, change.Path);
                if (File.Exists(fullpath))
                    await new Commands.Remove(_repo.FullPath, [change.Path]).ExecAsync();
            }
            else if (change.Index == Models.ChangeState.Renamed)
            {
                var old = Native.OS.GetAbsPath(_repo.FullPath, change.OriginalPath);
                if (File.Exists(old))
                    await new Commands.Remove(_repo.FullPath, [change.OriginalPath]).ExecAsync();

                await new Commands.Checkout(_repo.FullPath).FileWithRevisionAsync(change.Path, ToHead.SHA);
            }
            else
            {
                await new Commands.Checkout(_repo.FullPath).FileWithRevisionAsync(change.Path, ToHead.SHA);
            }
        }

        public async Task ResetMultipleToLeftAsync(List<Models.Change> changes)
        {
            var checkouts = new List<string>();
            var removes = new List<string>();

            foreach (var c in changes)
            {
                if (c.Index == Models.ChangeState.Added)
                {
                    var fullpath = Native.OS.GetAbsPath(_repo.FullPath, c.Path);
                    if (File.Exists(fullpath))
                        removes.Add(c.Path);
                }
                else if (c.Index == Models.ChangeState.Renamed)
                {
                    var old = Native.OS.GetAbsPath(_repo.FullPath, c.Path);
                    if (File.Exists(old))
                        removes.Add(c.Path);

                    checkouts.Add(c.OriginalPath);
                }
                else
                {
                    checkouts.Add(c.Path);
                }
            }

            if (removes.Count > 0)
                await new Commands.Remove(_repo.FullPath, removes).ExecAsync();

            if (checkouts.Count > 0)
                await new Commands.Checkout(_repo.FullPath).MultipleFilesWithRevisionAsync(checkouts, _baseHead.SHA);
        }

        public async Task ResetMultipleToRightAsync(List<Models.Change> changes)
        {
            var checkouts = new List<string>();
            var removes = new List<string>();

            foreach (var c in changes)
            {
                if (c.Index == Models.ChangeState.Deleted)
                {
                    var fullpath = Native.OS.GetAbsPath(_repo.FullPath, c.Path);
                    if (File.Exists(fullpath))
                        removes.Add(c.Path);
                }
                else if (c.Index == Models.ChangeState.Renamed)
                {
                    var renamed = Native.OS.GetAbsPath(_repo.FullPath, c.OriginalPath);
                    if (File.Exists(renamed))
                        removes.Add(c.OriginalPath);

                    checkouts.Add(c.Path);
                }
                else
                {
                    checkouts.Add(c.Path);
                }
            }

            if (removes.Count > 0)
                await new Commands.Remove(_repo.FullPath, removes).ExecAsync();

            if (checkouts.Count > 0)
                await new Commands.Checkout(_repo.FullPath).MultipleFilesWithRevisionAsync(checkouts, _toHead.SHA);
        }

        public async Task<bool> SaveChangesAsPatchAsync(List<Models.Change> changes, string saveTo)
        {
            return await Commands.SaveChangesAsPatch.ProcessRevisionCompareChangesAsync(_repo.FullPath, changes, _based, _to, saveTo);
        }

        public void CherryPick(List<Models.Commit> commits)
        {
            if (_repo.CanCreatePopup())
                _repo.ShowPopup(new CherryPick(_repo, commits));
        }

        private void UpdatePickableCommits()
        {
            IsLoadingPickableCommits = true;

            Task.Run(async () =>
            {
                var rightOnly = await new Commands.QueryPickableCommits(_repo.FullPath, _baseHead.SHA, _toHead.SHA)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                var leftOnly = await new Commands.QueryPickableCommits(_repo.FullPath, _toHead.SHA, _baseHead.SHA)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    LeftOnlyCommits = leftOnly;
                    RightOnlyCommits = rightOnly;
                    IsLoadingPickableCommits = false;
                });
            });
        }

        private void UpdateChanges()
        {
            IsLoadingChanges = true;
            VisibleChanges = [];
            SelectedChanges = [];

            Task.Run(async () =>
            {
                _changes = await new Commands.CompareRevisions(_repo.FullPath, _based, _to)
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
                    TotalChanges = _changes.Count;
                    VisibleChanges = visible;
                    IsLoadingChanges = false;

                    if (VisibleChanges.Count > 0)
                        SelectedChanges = [VisibleChanges[0]];
                    else
                        SelectedChanges = [];
                });
            });
        }

        private void RefreshVisibleChanges()
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

        private string GetName(object obj)
        {
            return obj switch
            {
                Models.Branch b => b.FriendlyName,
                Models.Tag t => t.Name,
                Models.Commit c => c.SHA.Substring(0, 10),
                _ => "HEAD",
            };
        }

        private string GetSHA(object obj)
        {
            return obj switch
            {
                Models.Branch b => b.Head,
                Models.Tag t => t.SHA,
                Models.Commit c => c.SHA,
                _ => "HEAD",
            };
        }

        private Repository _repo;
        private bool _isLoadingChanges = true;
        private bool _isLoadingPickableCommits = true;
        private bool _isViewChanges = true;
        private bool _canResetFiles = false;
        private string _based = string.Empty;
        private string _to = string.Empty;
        private string _baseName = string.Empty;
        private string _toName = string.Empty;
        private Models.Commit _baseHead = null;
        private Models.Commit _toHead = null;
        private int _totalChanges = 0;
        private List<Models.Change> _changes = null;
        private List<Models.Change> _visibleChanges = null;
        private List<Models.Change> _selectedChanges = null;
        private List<Models.Commit> _leftOnlyCommits = [];
        private List<Models.Commit> _rightOnlyCommits = [];
        private string _searchFilter = string.Empty;
        private DiffContext _diffContext = null;
    }
}
