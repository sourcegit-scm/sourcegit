using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class CheckoutCommandPalette : ICommandPalette
    {
        public List<Models.Branch> Branches
        {
            get => _branches;
            private set => SetProperty(ref _branches, value);
        }

        public Models.Branch SelectedBranch
        {
            get => _selectedBranch;
            set => SetProperty(ref _selectedBranch, value);
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

        public CheckoutCommandPalette(Launcher launcher, Repository repo)
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
            _selectedBranch = null;
            _filter = null;
        }

        public void ClearFilter()
        {
            Filter = string.Empty;
        }

        public async Task ExecAsync()
        {
            _launcher.CommandPalette = null;

            if (_selectedBranch != null)
                await _repo.CheckoutBranchAsync(_selectedBranch);

            Dispose();
            GC.Collect();
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

        private Launcher _launcher;
        private Repository _repo;
        private List<Models.Branch> _branches = [];
        private Models.Branch _selectedBranch = null;
        private string _filter;
    }
}
