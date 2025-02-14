using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
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
                if (SetProperty(ref _selectedLocalBranch, value))
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
                if (SetProperty(ref _selectedRemote, value))
                    AutoSelectBranchByRemote();
            }
        }

        public Models.Branch SelectedRemoteBranch
        {
            get => _selectedRemoteBranch;
            private set => SetProperty(ref _selectedRemoteBranch, value);
        }
        
        [Required(ErrorMessage = "Remote branch is required!!!")]
        public string SelectedRemoteBranchName
        {
            get => _selectedRemoteBranchName;
            set
            {
                if (SetProperty(ref _selectedRemoteBranchName, value))
                {
                    SelectedRemoteBranch = null;

                    foreach (var branch in _repo.Branches.Where(branch => branch.Remote == _selectedRemote.Name))
                    {
                        if (_selectedRemoteBranchName == branch.Name)
                        {
                            SelectedRemoteBranch = branch;
                        }
                    }

                    IsSetTrackOptionVisible = SelectedRemoteBranch != null && _selectedLocalBranch.Upstream != SelectedRemoteBranch.FullName;
                }
            }
        }

        public bool IsSetTrackOptionVisible
        {
            get => _isSetTrackOptionVisible;
            private set => SetProperty(ref _isSetTrackOptionVisible, value);
        }

        public bool Tracking
        {
            get;
            set;
        } = true;

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
            var current = null as Models.Branch;
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
                _selectedLocalBranch = localBranch;
                HasSpecifiedLocalBranch = true;
            }
            else
            {
                _selectedLocalBranch = current;
                HasSpecifiedLocalBranch = false;
            }

            // Find preferred remote if selected local branch has upstream.
            if (!string.IsNullOrEmpty(_selectedLocalBranch?.Upstream))
            {
                foreach (var branch in repo.Branches)
                {
                    if (!branch.IsLocal && _selectedLocalBranch.Upstream == branch.FullName)
                    {
                        _selectedRemote = repo.Remotes.Find(remote => remote.Name == branch.Remote);
                        break;
                    }
                }
            }

            // Set default remote to the first if it has not been set.
            if (_selectedRemote == null)
            {
                var remote = null as Models.Remote;
                if (!string.IsNullOrEmpty(_repo.Settings.DefaultRemote))
                    remote = repo.Remotes.Find(remote => remote.Name == _repo.Settings.DefaultRemote);

                _selectedRemote = remote ?? repo.Remotes[0];
            }

            // Auto select preferred remote branch.
            AutoSelectBranchByRemote();

            View = new Views.Push() { DataContext = this };
        }

        public override bool CanStartDirectly()
        {
            return !string.IsNullOrEmpty(SelectedRemoteBranch?.Head);
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);

            ProgressDescription =
                $"Push {_selectedLocalBranch.Name} -> {_selectedRemote.Name}/{_selectedRemoteBranchName} ...";

            return Task.Run(() =>
            {
                var succ = new Commands.Push(
                    _repo.FullPath,
                    _selectedLocalBranch.Name,
                    _selectedRemote.Name,
                    _selectedRemoteBranchName,
                    PushAllTags,
                    _repo.Submodules.Count > 0 && CheckSubmodules,
                    _isSetTrackOptionVisible && Tracking,
                    ForcePush,
                    SetProgressDescription).Exec();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private void AutoSelectBranchByRemote()
        {
            // If selected local branch has upstream. Try to find it in current remote branches.
            if (!string.IsNullOrEmpty(_selectedLocalBranch.Upstream))
            {
                foreach (var branch in _repo.Branches.Where(branch => branch.Remote == _selectedRemote.Name))
                {
                    if (_selectedLocalBranch.Upstream == branch.FullName)
                    {
                        SelectedRemoteBranchName = branch.Name;
                        return;
                    }
                }
            }

            // Try to find a remote branch with the same name of selected local branch.
            foreach (var branch in _repo.Branches.Where(branch => branch.Remote == _selectedRemote.Name))
            {
                if (_selectedLocalBranch.Name == branch.Name)
                {
                    SelectedRemoteBranchName = branch.Name;
                    return;
                }
            }

            SelectedRemoteBranchName = _selectedLocalBranch.Name;
        }

        private readonly Repository _repo = null;
        private Models.Branch _selectedLocalBranch = null;
        private Models.Remote _selectedRemote = null;
        private string _selectedRemoteBranchName = null;
        private Models.Branch _selectedRemoteBranch = null;
        private bool _isSetTrackOptionVisible = false;
    }
}
