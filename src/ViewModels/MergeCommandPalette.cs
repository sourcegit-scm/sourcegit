using System;
using System.Collections.Generic;

namespace SourceGit.ViewModels
{
    public class MergeCommandPalette : ICommandPalette
    {
        public List<Models.Branch> Branches
        {
            get => _branches;
            private set => SetProperty(ref _branches, value);
        }

        public string Filter
        {
            get => _filter;
            set
            {
                if (SetProperty(ref _filter, value))
                    UpdateBranches();
            }
        }

        public Models.Branch SelectedBranch
        {
            get => _selectedBranch;
            set => SetProperty(ref _selectedBranch, value);
        }

        public MergeCommandPalette(Launcher launcher, Repository repo)
        {
            _launcher = launcher;
            _repo = repo;
            UpdateBranches();
        }

        public override void Cleanup()
        {
            _launcher = null;
            _repo = null;
            _branches.Clear();
            _filter = null;
            _selectedBranch = null;
        }

        public void ClearFilter()
        {
            Filter = string.Empty;
        }

        public void Launch()
        {
            if (_repo.CanCreatePopup() && _selectedBranch != null)
                _repo.ShowPopup(new Merge(_repo, _selectedBranch, _repo.CurrentBranch.Name, false));

            _launcher?.CancelCommandPalette();
        }

        private void UpdateBranches()
        {
            var current = _repo.CurrentBranch;
            if (current == null)
                return;

            var branches = new List<Models.Branch>();
            foreach (var b in _repo.Branches)
            {
                if (b == current)
                    continue;

                if (string.IsNullOrEmpty(_filter) || b.FriendlyName.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                    branches.Add(b);
            }

            branches.Sort((l, r) =>
            {
                if (l.IsLocal == r.IsLocal)
                    return Models.NumericSort.Compare(l.Name, r.Name);

                return l.IsLocal ? -1 : 1;
            });

            var autoSelected = _selectedBranch;
            if (branches.Count == 0)
                autoSelected = null;
            else if (_selectedBranch == null || !branches.Contains(_selectedBranch))
                autoSelected = branches[0];

            Branches = branches;
            SelectedBranch = autoSelected;
        }

        private Launcher _launcher = null;
        private Repository _repo = null;
        private List<Models.Branch> _branches = new List<Models.Branch>();
        private string _filter = string.Empty;
        private Models.Branch _selectedBranch = null;
    }
}
