using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace SourceGit.ViewModels
{
    public class ReflogCommandPalette : ICommandPalette
    {
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public List<Models.ReflogEntry> VisibleEntries
        {
            get => _visibleEntries;
            private set => SetProperty(ref _visibleEntries, value);
        }

        public Models.ReflogEntry SelectedEntry
        {
            get => _selectedEntry;
            set => SetProperty(ref _selectedEntry, value);
        }

        public string Filter
        {
            get => _filter;
            set
            {
                if (SetProperty(ref _filter, value))
                    UpdateVisible();
            }
        }

        public ReflogCommandPalette(Repository repo)
        {
            _repo = repo;
            _isLoading = true;

            Task.Run(async () =>
            {
                var entries = await new Commands.QueryReflog(_repo.FullPath)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    IsLoading = false;
                    _entries = entries;
                    UpdateVisible();
                });
            });
        }

        public void ClearFilter()
        {
            Filter = string.Empty;
        }

        public void NavigateTo()
        {
            var entry = _selectedEntry;
            Close();

            if (entry != null)
                _repo.NavigateToCommit(entry.SHA);
        }

        public async Task ResetToAsync(Models.ReflogEntry entry)
        {
            Close();

            var current = _repo.CurrentBranch;
            if (entry == null || current == null || !_repo.CanCreatePopup())
                return;

            var commit = await new Commands.QuerySingleCommit(_repo.FullPath, entry.SHA).GetResultAsync();
            if (commit != null)
                _repo.ShowPopup(new Reset(_repo, current, commit));
        }

        private void UpdateVisible()
        {
            if (string.IsNullOrEmpty(_filter))
            {
                VisibleEntries = _entries;
            }
            else
            {
                var visible = new List<Models.ReflogEntry>();
                foreach (var entry in _entries)
                {
                    if (entry.SHA.StartsWith(_filter, StringComparison.OrdinalIgnoreCase) ||
                        entry.Selector.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                        entry.Message.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(entry);
                }

                VisibleEntries = visible;
            }

            if (_visibleEntries.Count == 0)
                SelectedEntry = null;
            else if (_selectedEntry == null || !_visibleEntries.Contains(_selectedEntry))
                SelectedEntry = _visibleEntries[0];
        }

        private Repository _repo = null;
        private bool _isLoading = false;
        private List<Models.ReflogEntry> _entries = [];
        private List<Models.ReflogEntry> _visibleEntries = [];
        private Models.ReflogEntry _selectedEntry = null;
        private string _filter = string.Empty;
    }
}
