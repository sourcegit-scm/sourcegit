using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
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

        public List<Models.Branch> LocalBranches
        {
            get;
        }

        [Required(ErrorMessage = "Local branch is required!!!")]
        public Models.Branch SelectedLocalBranch
        {
            get => _selectedLocalBranch;
            set
            {
                if (SetProperty(ref _selectedLocalBranch, value, true))
                    PostLocalBranchChanged();
            }
        }

        public List<Models.Remote> Remotes
        {
            get => _remotes;
            private set => SetProperty(ref _remotes, value);
        }

        [Required(ErrorMessage = "Remote is required!!!")]
        public Models.Remote SelectedRemote
        {
            get => _selectedRemote;
            set
            {
                if (SetProperty(ref _selectedRemote, value, true))
                    PostSelectedRemoteChanged();
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
                {
                    IsSetTrackOptionVisible = !string.IsNullOrEmpty(_selectedRemote.URL)
                        && value != null
                        && (value.Head == null || _selectedLocalBranch.Upstream != value.FullName);
                    Tracking = true;
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
            get => _repo.UIStates.PushAllTags;
            set => _repo.UIStates.PushAllTags = value;
        }

        public bool ForcePush
        {
            get;
            set;
        }

        public bool NoVerify
        {
            get;
            set;
        }

        public Push(Repository repo, Models.Branch localBranch)
        {
            _repo = repo;
            CanTerminate = true;

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

                HasSpecifiedLocalBranch = true;
                SelectedLocalBranch = localBranch;
            }
            else
            {
                HasSpecifiedLocalBranch = false;
                SelectedLocalBranch = current;
            }
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
            ProgressDescription = $"Push {_selectedLocalBranch.Name} -> {_selectedRemoteBranch.FriendlyName} ...";

            var log = _repo.CreateLog("Push");
            Use(log);

            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;

            var succ = await new Commands.Push(
                _repo.FullPath,
                _selectedLocalBranch,
                _selectedRemote,
                _selectedRemoteBranch,
                PushAllTags,
                _repo.Submodules.Count > 0 && CheckSubmodules,
                _isSetTrackOptionVisible && _tracking,
                ForcePush,
                NoVerify).WithCancellation(token).Use(log).ExecAsync();

            log.Complete();

            _cancellation = null;
            return succ;
        }

        public override void Terminate()
        {
            // Just fire cancel event and UI will auto wait the `Sure` complete
            var _ = _cancellation?.CancelAsync();
        }

        private void PostLocalBranchChanged()
        {
            if (_selectedLocalBranch == null)
                return;

            var remotes = new List<Models.Remote>();
            remotes.AddRange(_repo.Remotes);

            // Respect the `branch.<name>.pushRemote` settings.
            var pushRemote = new Commands.Config(_repo.FullPath).Get($"branch.\"{_selectedLocalBranch.Name}\".pushRemote");
            if (!string.IsNullOrEmpty(pushRemote))
            {
                var remote = remotes.Find(x => x.URL.Equals(pushRemote, StringComparison.Ordinal));
                if (remote == null)
                {
                    var extra = new Models.Remote() { Name = pushRemote };
                    remotes.Add(extra);

                    Remotes = remotes;
                    ForceUpdateSelectedRemote(extra);
                }
                else
                {
                    // Force to trigger `PostRemoteChanged` even if the remote is the same as before.
                    Remotes = remotes;
                    ForceUpdateSelectedRemote(remote);
                }

                return;
            }

            // Update remotes list.
            Remotes = remotes;

            // Try to select remote by upstream branch.
            var upstream = _selectedLocalBranch.Upstream;
            if (!string.IsNullOrEmpty(upstream) && !_selectedLocalBranch.IsUpstreamGone)
            {
                foreach (var branch in _repo.Branches)
                {
                    if (!branch.IsLocal && upstream.Equals(branch.FullName, StringComparison.Ordinal))
                    {
                        ForceUpdateSelectedRemote(Remotes.Find(x => x.Name == branch.Remote));
                        return;
                    }
                }
            }

            // Fallback to select the first remote.
            if (Remotes.Count > 0)
            {
                Models.Remote fallback = null;
                if (!string.IsNullOrEmpty(_repo.Settings.DefaultRemote))
                    fallback = Remotes.Find(x => x.Name.Equals(_repo.Settings.DefaultRemote, StringComparison.Ordinal));

                ForceUpdateSelectedRemote(fallback ?? Remotes[0]);
            }
        }

        private void ForceUpdateSelectedRemote(Models.Remote remote)
        {
            var old = _selectedRemote;
            SelectedRemote = remote;
            if (remote == old)
                PostSelectedRemoteChanged();
        }

        private void PostSelectedRemoteChanged()
        {
            if (_selectedRemote == null || _selectedLocalBranch == null)
                return;

            // Gather branches.
            var branches = new List<Models.Branch>();
            foreach (var branch in _repo.Branches)
            {
                if (!branch.IsLocal && _selectedRemote.Name.Equals(branch.Remote, StringComparison.Ordinal))
                    branches.Add(branch);
            }

            // Check `branch.<name>.merge` configuration if selected remote comes from an extra `branch.<name>.pushRemote`
            if (string.IsNullOrEmpty(_selectedRemote.URL))
            {
                var mergeTarget = new Commands.Config(_repo.FullPath).Get($"branch.\"{_selectedLocalBranch.Name}\".merge");
                if (!string.IsNullOrEmpty(mergeTarget))
                {
                    var target = new Models.Branch()
                    {
                        Name = mergeTarget.StartsWith("refs/heads/") ? mergeTarget.Substring(11) : mergeTarget,
                        Remote = _selectedRemote.Name,
                        Head = "Unknown (but used in View)",
                    };
                    branches.Add(target);
                    RemoteBranches = branches;
                    SelectedRemoteBranch = target;
                    return;
                }
            }

            // If selected local branch has upstream. Try to find it in current remote branches.
            if (!string.IsNullOrEmpty(_selectedLocalBranch.Upstream))
            {
                foreach (var branch in branches)
                {
                    if (_selectedLocalBranch.Upstream.Equals(branch.FullName, StringComparison.Ordinal))
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
                if (_selectedLocalBranch.Name.Equals(branch.Name, StringComparison.Ordinal))
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
        private List<Models.Remote> _remotes = null;
        private Models.Branch _selectedLocalBranch = null;
        private Models.Remote _selectedRemote = null;
        private List<Models.Branch> _remoteBranches = [];
        private Models.Branch _selectedRemoteBranch = null;
        private bool _isSetTrackOptionVisible = false;
        private bool _tracking = true;
        private CancellationTokenSource _cancellation = null;
    }
}
