using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Push : Popup
    {
        public bool HasSpecifiedLocalBranch
        {
            get;
            private set;
        }

        [Required(ErrorMessage = "Local branch is required!!!")]
        public Models.Branch SelectedLocalBranch
        {
            get => _selectedLocalBranch;
            set
            {
                if (SetProperty(ref _selectedLocalBranch, value, true))
                    AutoSelectBranchByRemote();
            }
        }

        public List<Models.Branch> LocalBranches
        {
            get;
        }

        public List<Models.Remote> Remotes
        {
            get => _repo.Remotes;
        }

        [Required(ErrorMessage = "Remote is required!!!")]
        public Models.Remote SelectedRemote
        {
            get => _selectedRemote;
            set
            {
                if (SetProperty(ref _selectedRemote, value, true))
                    AutoSelectBranchByRemote();
            }
        }

        public List<Models.Branch> RemoteBranches
        {
            get => _remoteBranches;
            private set => SetProperty(ref _remoteBranches, value);
        }

        [Required(ErrorMessage = "Remote branch is required!!!")]
        public Models.Branch SelectedRemoteBranch
        {
            get => _selectedRemoteBranch;
            set
            {
                if (SetProperty(ref _selectedRemoteBranch, value, true))
                    IsSetTrackOptionVisible = value != null && _selectedLocalBranch.Upstream != value.FullName;
            }
        }

        public bool IsSetTrackOptionVisible
        {
            get => _isSetTrackOptionVisible;
            private set => SetProperty(ref _isSetTrackOptionVisible, value);
        }

        public bool Tracking
        {
            get => _tracking;
            set => SetProperty(ref _tracking, value);
        }

        public bool IsCheckSubmodulesVisible
        {
            get => _repo.Submodules.Count > 0;
        }

        public bool CheckSubmodules
        {
            get;
            set;
        } = true;

        public bool PushAllTags
        {
            get => _repo.Settings.PushAllTags;
            set => _repo.Settings.PushAllTags = value;
        }

        public bool ForcePush
        {
            get;
            set;
        }

        public Push(Repository repo, Models.Branch localBranch)
        {
            _repo = repo;

            // Gather all local branches and find current branch.
            LocalBranches = new List<Models.Branch>();
            Models.Branch current = null;
            foreach (var branch in _repo.Branches)
            {
                if (branch.IsLocal)
                    LocalBranches.Add(branch);
                if (branch.IsCurrent)
                    current = branch;
            }

            // Set default selected local branch.
            if (localBranch != null)
            {
                if (LocalBranches.Count == 0)
                    LocalBranches.Add(localBranch);

                _selectedLocalBranch = localBranch;
                HasSpecifiedLocalBranch = true;
            }
            else
            {
                _selectedLocalBranch = current;
                HasSpecifiedLocalBranch = false;
            }

            // Find preferred remote if selected local branch has upstream.
            if (!string.IsNullOrEmpty(_selectedLocalBranch?.Upstream) && !_selectedLocalBranch.IsUpstreamGone)
            {
                _tracking = false;

                foreach (var branch in repo.Branches)
                {
                    if (!branch.IsLocal && _selectedLocalBranch.Upstream == branch.FullName)
                    {
                        _selectedRemote = repo.Remotes.Find(x => x.Name == branch.Remote);
                        break;
                    }
                }
            }
            else
            {
                _tracking = true;
            }

            // Set default remote to the first if it has not been set.
            if (_selectedRemote == null)
            {
                Models.Remote remote = null;
                if (!string.IsNullOrEmpty(_repo.Settings.DefaultRemote))
                    remote = repo.Remotes.Find(x => x.Name == _repo.Settings.DefaultRemote);

                _selectedRemote = remote ?? repo.Remotes[0];
            }

            // Auto select preferred remote branch.
            AutoSelectBranchByRemote();
        }

        public void PushToNewBranch(string name)
        {
            var exist = _remoteBranches.Find(x => x.Name.Equals(name, StringComparison.Ordinal));
            if (exist != null)
            {
                SelectedRemoteBranch = exist;
                return;
            }

            var fake = new Models.Branch()
            {
                Name = name,
                Remote = _selectedRemote.Name,
            };
            var collection = new List<Models.Branch>();
            collection.AddRange(_remoteBranches);
            collection.Add(fake);
            RemoteBranches = collection;
            SelectedRemoteBranch = fake;
        }

        public override bool CanStartDirectly()
        {
            return !string.IsNullOrEmpty(_selectedRemoteBranch?.Head);
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();

            var remoteBranchName = _selectedRemoteBranch.Name;
            ProgressDescription = $"Push {_selectedLocalBranch.Name} -> {_selectedRemote.Name}/{remoteBranchName} ...";

            var log = _repo.CreateLog("Push");
            Use(log);

            var succ = await new Commands.Push(
                _repo.FullPath,
                _selectedLocalBranch.Name,
                _selectedRemote.Name,
                remoteBranchName,
                PushAllTags,
                _repo.Submodules.Count > 0 && CheckSubmodules,
                _isSetTrackOptionVisible && _tracking,
                ForcePush).Use(log).RunAsync();

            log.Complete();
            return succ;
        }

        private void AutoSelectBranchByRemote()
        {
            // Gather branches.
            var branches = new List<Models.Branch>();
            foreach (var branch in _repo.Branches)
            {
                if (branch.Remote == _selectedRemote.Name)
                    branches.Add(branch);
            }

            // If selected local branch has upstream. Try to find it in current remote branches.
            if (!string.IsNullOrEmpty(_selectedLocalBranch.Upstream))
            {
                foreach (var branch in branches)
                {
                    if (_selectedLocalBranch.Upstream == branch.FullName)
                    {
                        RemoteBranches = branches;
                        SelectedRemoteBranch = branch;
                        return;
                    }
                }
            }

            // Try to find a remote branch with the same name of selected local branch.
            foreach (var branch in branches)
            {
                if (_selectedLocalBranch.Name == branch.Name)
                {
                    RemoteBranches = branches;
                    SelectedRemoteBranch = branch;
                    return;
                }
            }

            // Add a fake new branch.
            var fake = new Models.Branch()
            {
                Name = _selectedLocalBranch.Name,
                Remote = _selectedRemote.Name,
            };
            branches.Add(fake);
            RemoteBranches = branches;
            SelectedRemoteBranch = fake;
        }

        private readonly Repository _repo = null;
        private Models.Branch _selectedLocalBranch = null;
        private Models.Remote _selectedRemote = null;
        private List<Models.Branch> _remoteBranches = [];
        private Models.Branch _selectedRemoteBranch = null;
        private bool _isSetTrackOptionVisible = false;
        private bool _tracking = true;
    }
}
