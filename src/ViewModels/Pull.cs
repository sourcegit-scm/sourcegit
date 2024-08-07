using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Pull : Popup
    {
        public List<Models.Remote> Remotes => _repo.Remotes;
        public Models.Branch Current => _current;

        public bool HasSpecifiedRemoteBranch
        {
            get;
            private set;
        }

        public Models.Remote SelectedRemote
        {
            get => _selectedRemote;
            set
            {
                if (SetProperty(ref _selectedRemote, value))
                {
                    var branches = new List<Models.Branch>();
                    foreach (var branch in _repo.Branches)
                    {
                        if (branch.Remote == value.Name)
                            branches.Add(branch);
                    }
                    RemoteBranches = branches;
                    SelectedBranch = branches.Count > 0 ? branches[0] : null;
                }
            }
        }

        public List<Models.Branch> RemoteBranches
        {
            get => _remoteBranches;
            private set => SetProperty(ref _remoteBranches, value);
        }

        [Required(ErrorMessage = "Remote branch to pull is required!!!")]
        public Models.Branch SelectedBranch
        {
            get => _selectedBranch;
            set => SetProperty(ref _selectedBranch, value);
        }

        public Models.DealWithLocalChanges PreAction
        {
            get => _repo.Settings.DealWithLocalChangesOnPull;
            set => _repo.Settings.DealWithLocalChangesOnPull = value;
        }

        public bool UseRebase
        {
            get => _repo.Settings.PreferRebaseInsteadOfMerge;
            set => _repo.Settings.PreferRebaseInsteadOfMerge = value;
        }

        public bool FetchAllBranches
        {
            get => _repo.Settings.FetchAllBranchesOnPull;
            set => _repo.Settings.FetchAllBranchesOnPull = value;
        }

        public bool NoTags
        {
            get => _repo.Settings.FetchWithoutTagsOnPull;
            set => _repo.Settings.FetchWithoutTagsOnPull = value;
        }

        public Pull(Repository repo, Models.Branch specifiedRemoteBranch)
        {
            _repo = repo;
            _current = repo.CurrentBranch;

            if (specifiedRemoteBranch != null)
            {
                _selectedRemote = repo.Remotes.Find(x => x.Name == specifiedRemoteBranch.Remote);
                _selectedBranch = specifiedRemoteBranch;
                HasSpecifiedRemoteBranch = true;
            }
            else
            {
                if (!string.IsNullOrEmpty(_current.Upstream))
                {
                    foreach (var branch in repo.Branches)
                    {
                        if (!branch.IsLocal && _current.Upstream == branch.FullName)
                        {
                            _selectedRemote = repo.Remotes.Find(x => x.Name == branch.Remote);
                            _selectedBranch = branch;
                            break;
                        }
                    }
                }

                HasSpecifiedRemoteBranch = false;
            }

            // Make sure remote is exists.
            if (_selectedRemote == null)
            {
                _selectedRemote = repo.Remotes[0];
                _selectedBranch = null;
                HasSpecifiedRemoteBranch = false;
            }

            _remoteBranches = new List<Models.Branch>();
            foreach (var branch in _repo.Branches)
            {
                if (branch.Remote == _selectedRemote.Name)
                    _remoteBranches.Add(branch);
            }

            if (_selectedBranch == null && _remoteBranches.Count > 0)
            {
                _selectedBranch = _remoteBranches[0];
            }

            View = new Views.Pull() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);

            return Task.Run(() =>
            {
                var changes = new Commands.CountLocalChangesWithoutUntracked(_repo.FullPath).Result();
                var needPopStash = false;
                if (changes > 0)
                {
                    if (PreAction == Models.DealWithLocalChanges.StashAndReaply)
                    {
                        SetProgressDescription("Stash local changes...");
                        var succ = new Commands.Stash(_repo.FullPath).Push("PULL_AUTO_STASH");
                        if (!succ)
                        {
                            CallUIThread(() => _repo.SetWatcherEnabled(true));
                            return false;
                        }

                        needPopStash = true;
                    }
                    else if (PreAction == Models.DealWithLocalChanges.Discard)
                    {
                        SetProgressDescription("Discard local changes ...");
                        Commands.Discard.All(_repo.FullPath);
                    }
                }

                var rs = false;
                if (FetchAllBranches)
                {
                    SetProgressDescription($"Fetching remote: {_selectedRemote.Name}...");
                    rs = new Commands.Fetch(_repo.FullPath, _selectedRemote.Name, false, NoTags, SetProgressDescription).Exec();
                    if (!rs)
                        return false;

                    // Use merge/rebase instead of pull as fetch is done manually.
                    if (UseRebase)
                    {
                        SetProgressDescription($"Rebase {_current.Name} on {_selectedBranch.FriendlyName} ...");
                        rs = new Commands.Rebase(_repo.FullPath, _selectedBranch.FriendlyName, false).Exec();
                    }
                    else
                    {
                        SetProgressDescription($"Merge {_selectedBranch.FriendlyName} into {_current.Name} ...");
                        rs = new Commands.Merge(_repo.FullPath, _selectedBranch.FriendlyName, "", SetProgressDescription).Exec();
                    }
                }
                else
                {
                    SetProgressDescription($"Pull {_selectedRemote.Name}/{_selectedBranch.Name}...");
                    rs = new Commands.Pull(_repo.FullPath, _selectedRemote.Name, _selectedBranch.Name, UseRebase, NoTags, SetProgressDescription).Exec();
                }

                if (rs && needPopStash)
                {
                    SetProgressDescription("Re-apply local changes...");
                    rs = new Commands.Stash(_repo.FullPath).Pop("stash@{0}");
                }

                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return rs;
            });
        }

        private readonly Repository _repo = null;
        private readonly Models.Branch _current = null;
        private Models.Remote _selectedRemote = null;
        private List<Models.Branch> _remoteBranches = null;
        private Models.Branch _selectedBranch = null;
    }
}
