using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Pull : Popup
    {
        public List<Models.Remote> Remotes => _repo.Remotes;
        public Models.Branch Current { get; }

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
                    PostRemoteSelected();
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
            set => SetProperty(ref _selectedBranch, value, true);
        }

        public bool DiscardLocalChanges
        {
            get;
            set;
        } = false;

        public bool UseRebase
        {
            get => _repo.Settings.PreferRebaseInsteadOfMerge;
            set => _repo.Settings.PreferRebaseInsteadOfMerge = value;
        }

        public bool IsRecurseSubmoduleVisible
        {
            get => _repo.Submodules.Count > 0;
        }

        public bool RecurseSubmodules
        {
            get => _repo.Settings.UpdateSubmodulesOnCheckoutBranch;
            set => _repo.Settings.UpdateSubmodulesOnCheckoutBranch = value;
        }

        public Pull(Repository repo, Models.Branch specifiedRemoteBranch)
        {
            _repo = repo;
            Current = repo.CurrentBranch;

            if (specifiedRemoteBranch != null)
            {
                _selectedRemote = repo.Remotes.Find(x => x.Name == specifiedRemoteBranch.Remote);
                _selectedBranch = specifiedRemoteBranch;

                var branches = new List<Models.Branch>();
                foreach (var branch in _repo.Branches)
                {
                    if (branch.Remote == specifiedRemoteBranch.Remote)
                        branches.Add(branch);
                }

                _remoteBranches = branches;
                HasSpecifiedRemoteBranch = true;
            }
            else
            {
                Models.Remote autoSelectedRemote = null;
                if (!string.IsNullOrEmpty(Current.Upstream))
                {
                    var remoteNameEndIdx = Current.Upstream.IndexOf('/', 13);
                    if (remoteNameEndIdx > 0)
                    {
                        var remoteName = Current.Upstream.Substring(13, remoteNameEndIdx - 13);
                        autoSelectedRemote = _repo.Remotes.Find(x => x.Name == remoteName);
                    }
                }

                if (autoSelectedRemote == null)
                {
                    Models.Remote remote = null;
                    if (!string.IsNullOrEmpty(_repo.Settings.DefaultRemote))
                        remote = _repo.Remotes.Find(x => x.Name == _repo.Settings.DefaultRemote);
                    _selectedRemote = remote ?? _repo.Remotes[0];
                }
                else
                {
                    _selectedRemote = autoSelectedRemote;
                }

                PostRemoteSelected();
                HasSpecifiedRemoteBranch = false;
            }
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();

            var log = _repo.CreateLog("Pull");
            Use(log);

            var updateSubmodules = IsRecurseSubmoduleVisible && RecurseSubmodules;
            var changes = await new Commands.CountLocalChanges(_repo.FullPath, false).GetResultAsync();
            var needPopStash = false;
            if (changes > 0)
            {
                if (DiscardLocalChanges)
                {
                    await Commands.Discard.AllAsync(_repo.FullPath, false, false, log);
                }
                else
                {
                    var succ = await new Commands.Stash(_repo.FullPath).Use(log).PushAsync("PULL_AUTO_STASH");
                    if (!succ)
                    {
                        log.Complete();
                        return false;
                    }

                    needPopStash = true;
                }
            }

            bool rs = await new Commands.Pull(
                _repo.FullPath,
                _selectedRemote.Name,
                !string.IsNullOrEmpty(Current.Upstream) && Current.Upstream.Equals(_selectedBranch.FullName) ? string.Empty : _selectedBranch.Name,
                UseRebase).Use(log).RunAsync();
            if (rs)
            {
                if (updateSubmodules)
                {
                    var submodules = await new Commands.QueryUpdatableSubmodules(_repo.FullPath).GetResultAsync();
                    if (submodules.Count > 0)
                        await new Commands.Submodule(_repo.FullPath).Use(log).UpdateAsync(submodules, true, true);
                }

                if (needPopStash)
                    await new Commands.Stash(_repo.FullPath).Use(log).PopAsync("stash@{0}");
            }

            log.Complete();

            if (_repo.SelectedViewIndex == 0)
            {
                var head = await new Commands.QueryRevisionByRefName(_repo.FullPath, "HEAD").GetResultAsync();
                _repo.NavigateToCommit(head, true);
            }

            return rs;
        }

        private void PostRemoteSelected()
        {
            var remoteName = _selectedRemote.Name;
            var branches = new List<Models.Branch>();
            foreach (var branch in _repo.Branches)
            {
                if (branch.Remote == remoteName)
                    branches.Add(branch);
            }

            RemoteBranches = branches;

            var autoSelectedBranch = false;
            if (!string.IsNullOrEmpty(Current.Upstream) &&
                Current.Upstream.StartsWith($"refs/remotes/{remoteName}/", System.StringComparison.Ordinal))
            {
                foreach (var branch in branches)
                {
                    if (Current.Upstream == branch.FullName)
                    {
                        SelectedBranch = branch;
                        autoSelectedBranch = true;
                        break;
                    }
                }
            }

            if (!autoSelectedBranch)
            {
                foreach (var branch in branches)
                {
                    if (Current.Name == branch.Name)
                    {
                        SelectedBranch = branch;
                        autoSelectedBranch = true;
                        break;
                    }
                }
            }

            if (!autoSelectedBranch)
                SelectedBranch = null;
        }

        private readonly Repository _repo = null;
        private Models.Remote _selectedRemote = null;
        private List<Models.Branch> _remoteBranches = null;
        private Models.Branch _selectedBranch = null;
    }
}
