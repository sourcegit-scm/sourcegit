using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class Push : Popup {
        public bool HasSpecifiedLocalBranch {
            get;
            private set;
        }

        [Required(ErrorMessage = "Local branch is required!!!")]
        public Models.Branch SelectedLocalBranch {
            get => _selectedLocalBranch;
            set {
                if (SetProperty(ref _selectedLocalBranch, value)) {
                    // If selected local branch has upstream branch. Try to find it's remote.
                    if (!string.IsNullOrEmpty(value.Upstream)) {
                        var branch = _repo.Branches.Find(x => x.FullName == value.Upstream);
                        if (branch != null) {
                            var remote = _repo.Remotes.Find(x => x.Name == branch.Remote);
                            if (remote != null && remote != _selectedRemote) {
                                SelectedRemote = remote;
                                return;
                            }
                        }
                    }

                    // Re-generate remote branches and auto-select remote branches.
                    AutoSelectBranchByRemote();
                }
            }
        }

        public List<Models.Branch> LocalBranches {
            get;
            private set;
        }

        public List<Models.Remote> Remotes {
            get => _repo.Remotes;
        }

        [Required(ErrorMessage = "Remote is required!!!")]
        public Models.Remote SelectedRemote {
            get => _selectedRemote;
            set {
                if (SetProperty(ref _selectedRemote, value)) AutoSelectBranchByRemote();
            }
        }

        public List<Models.Branch> RemoteBranches {
            get => _remoteBranches;
            private set => SetProperty(ref _remoteBranches, value);
        }

        [Required(ErrorMessage = "Remote branch is required!!!")]
        public Models.Branch SelectedRemoteBranch {
            get => _selectedRemoteBranch;
            set => SetProperty(ref _selectedRemoteBranch, value);
        }

        public bool PushAllTags {
            get;
            set;
        }

        public bool ForcePush {
            get;
            set;
        }

        public Push(Repository repo, Models.Branch localBranch) {
            _repo = repo;

            // Gather all local branches and find current branch.
            LocalBranches = new List<Models.Branch>();
            var current = null as Models.Branch;
            foreach (var branch in _repo.Branches) {
                if (branch.IsLocal) LocalBranches.Add(branch);
                if (branch.IsCurrent) current = branch;
            }

            // Set default selected local branch.
            if (localBranch != null) {
                _selectedLocalBranch = localBranch;
                HasSpecifiedLocalBranch = true;
            } else {
                _selectedLocalBranch = current;
                HasSpecifiedLocalBranch = false;
            }

            // Find preferred remote if selected local branch has upstream.
            if (!string.IsNullOrEmpty(_selectedLocalBranch.Upstream)) {
                foreach (var branch in repo.Branches) {
                    if (!branch.IsLocal && _selectedLocalBranch.Upstream == branch.FullName) {
                        _selectedRemote = repo.Remotes.Find(x => x.Name == branch.Remote);
                        break;
                    }
                }
            }

            // Set default remote to the first if haven't been set.
            if (_selectedRemote == null) _selectedRemote = repo.Remotes[0];

            // Auto select preferred remote branch.
            AutoSelectBranchByRemote();

            View = new Views.Push() { DataContext = this };
        }

        public override Task<bool> Sure() {
            _repo.SetWatcherEnabled(false);
            return Task.Run(() => {
                var remoteBranchName = _selectedRemoteBranch.Name.Replace(" (new)", "");
                SetProgressDescription($"Push {_selectedLocalBranch.Name} -> {_selectedRemote.Name}/{remoteBranchName} ...");
                var succ = new Commands.Push(
                    _repo.FullPath,
                    _selectedLocalBranch.Name,
                    _selectedRemote.Name,
                    remoteBranchName,
                    PushAllTags,
                    ForcePush,
                    string.IsNullOrEmpty(_selectedLocalBranch.Upstream),
                    SetProgressDescription).Exec();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private void AutoSelectBranchByRemote() {
            // Gather branches.
            var branches = new List<Models.Branch>();
            foreach (var branch in _repo.Branches) {
                if (branch.Remote == _selectedRemote.Name) branches.Add(branch);
            }

            // If selected local branch has upstream branch. Try to find it in current remote branches.
            if (!string.IsNullOrEmpty(_selectedLocalBranch.Upstream)) {
                foreach (var branch in branches) {
                    if (_selectedLocalBranch.Upstream == branch.FullName) {
                        RemoteBranches = branches;
                        SelectedRemoteBranch = branch;
                        return;
                    }
                }
            }

            // Find best remote branch by name.
            foreach (var branch in branches) {
                if (_selectedLocalBranch.Name == branch.Name) {
                    RemoteBranches = branches;
                    SelectedRemoteBranch = branch;
                    return;
                }
            }

            // Add a fake new branch.
            var fake = new Models.Branch() {
                Name = $"{_selectedLocalBranch.Name} (new)",
                Remote = _selectedRemote.Name,
            };
            branches.Add(fake);
            RemoteBranches = branches;
            SelectedRemoteBranch = fake;
        }

        private Repository _repo = null;
        private Models.Branch _selectedLocalBranch = null;
        private Models.Remote _selectedRemote = null;
        private List<Models.Branch> _remoteBranches = new List<Models.Branch>();
        private Models.Branch _selectedRemoteBranch = null;
    }
}
