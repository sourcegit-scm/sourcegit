using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    /// <summary>
    /// Commit-detail-style change list for uncommitted (WIP) files. Diffs open on the timeline
    /// graph via <see cref="Histories.OpenMainDiff"/>, not inline.
    /// </summary>
    public partial class LocalChangesDetail : ObservableObject
    {
        public Repository Repository => _repo;

        public List<Models.Change> Changes
        {
            get => _changes;
            private set => SetProperty(ref _changes, value);
        }

        public List<Models.Change> VisibleChanges
        {
            get => _visibleChanges;
            private set => SetProperty(ref _visibleChanges, value);
        }

        public ChangeSelection ChangeSelection
        {
            get => _changeSelection;
            set
            {
                if (SetProperty(ref _changeSelection, value))
                    UpdateDetails();
            }
        }

        public string SearchChangeFilter
        {
            get => _searchChangeFilter;
            set
            {
                if (SetProperty(ref _searchChangeFilter, value))
                    RefreshVisibleChanges();
            }
        }

        public LocalChangesDetail(Repository repo)
        {
            _repo = repo;
            _repo.WorkingCopy.PropertyChanged += OnWorkingCopyPropertyChanged;
        }

        public void RefreshFromWorkingCopy()
        {
            var wc = _repo.WorkingCopy;
            _unstagedPaths = new HashSet<string>(wc.Unstaged.Select(c => c.Path), StringComparer.Ordinal);

            var all = new List<Models.Change>(wc.Staged.Count + wc.Unstaged.Count);
            all.AddRange(wc.Staged);
            all.AddRange(wc.Unstaged);
            Changes = all;
            RefreshVisibleChanges();
        }

        public void ClearSearchChangeFilter()
        {
            SearchChangeFilter = string.Empty;
        }

        public string GetAbsPath(string path)
        {
            return Native.OS.GetAbsPath(_repo.FullPath, path);
        }

        public void PresentMainDiff()
        {
            if (_diffContext == null || _repo.Histories == null)
                return;

            _repo.Histories.OpenMainDiff(_diffContext);
        }

        private void OnWorkingCopyPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is not (nameof(WorkingCopy.Staged) or nameof(WorkingCopy.Unstaged)))
                return;

            if (_repo.Histories?.DetailContext == this)
                RefreshFromWorkingCopy();
        }

        private void RefreshVisibleChanges()
        {
            if (string.IsNullOrEmpty(_searchChangeFilter))
            {
                VisibleChanges = _changes;
                return;
            }

            var filter = _searchChangeFilter;
            var visible = new List<Models.Change>();
            foreach (var c in _changes)
            {
                if (c.Path.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    c.OriginalPath.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    visible.Add(c);
            }

            VisibleChanges = visible;
        }

        private void UpdateDetails()
        {
            if (_changeSelection is not { Count: 1, HasFolder: false })
            {
                _diffContext = null;
                _repo.Histories?.CloseMainDiff();
                return;
            }

            var change = _changeSelection.Changes[0];
            if (change.IsConflicted)
            {
                _diffContext = null;
                _repo.Histories?.CloseMainDiff();
                return;
            }

            var isUnstaged = _unstagedPaths.Contains(change.Path);
            var context = new DiffContext(_repo.FullPath, new Models.DiffOption(change, isUnstaged), _diffContext);
            _diffContext = context;
            _repo.Histories?.OpenMainDiff(context);
        }

        private readonly Repository _repo;
        private List<Models.Change> _changes = [];
        private List<Models.Change> _visibleChanges = [];
        private ChangeSelection _changeSelection = new(null);
        private string _searchChangeFilter = string.Empty;
        private HashSet<string> _unstagedPaths = new(StringComparer.Ordinal);
        private DiffContext _diffContext = null;
    }
}
