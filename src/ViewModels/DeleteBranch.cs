using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DeleteBranch : Popup
    {
        public Models.Branch Target
        {
            get;
        }

        public Models.Branch TrackingRemoteBranch
        {
            get;
        }

        public string DeleteTrackingRemoteTip
        {
            get;
            private set;
        }

        public bool AlsoDeleteTrackingRemote
        {
            get => _alsoDeleteTrackingRemote;
            set => SetProperty(ref _alsoDeleteTrackingRemote, value);
        }

        public string DeleteWorktreeTip
        {
            get;
            private set;
        }

        public bool AlsoRemoveWorktree
        {
            get => _alsoRemoveWorktree;
            set => SetProperty(ref _alsoRemoveWorktree, value);
        }

        public bool HasWorktree
        {
            get;
            private set;
        }

        public DeleteBranch(Repository repo, Models.Branch branch)
        {
            _repo = repo;
            Target = branch;

            if (branch.IsLocal && !string.IsNullOrEmpty(branch.Upstream))
            {
                TrackingRemoteBranch = repo.Branches.Find(x => x.FullName == branch.Upstream);
                if (TrackingRemoteBranch != null)
                    DeleteTrackingRemoteTip = App.Text("DeleteBranch.WithTrackingRemote", TrackingRemoteBranch.FriendlyName);
            }

            if (branch.HasWorktree)
            {
                HasWorktree = true;
                DeleteWorktreeTip = App.Text("DeleteBranch.WithWorktree", branch.WorktreePath);
            }
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Deleting branch...";

            var log = _repo.CreateLog("Delete Branch");
            Use(log);

            if (Target.IsLocal)
            {
                var worktreePath = Target.WorktreePath;

                await new Commands.Branch(_repo.FullPath, Target.Name)
                    .Use(log)
                    .DeleteLocalAsync();
                _repo.UIStates.RemoveHistoryFilter(Target.FullName, Models.FilterType.LocalBranch);

                if (_alsoDeleteTrackingRemote && TrackingRemoteBranch != null)
                {
                    await DeleteRemoteBranchAsync(TrackingRemoteBranch, log);
                    _repo.UIStates.RemoveHistoryFilter(TrackingRemoteBranch.FullName, Models.FilterType.RemoteBranch);
                }

                if (_alsoRemoveWorktree && !string.IsNullOrEmpty(worktreePath))
                {
                    await new Commands.Worktree(_repo.FullPath)
                        .Use(log)
                        .RemoveAsync(worktreePath, false);
                    await new Commands.Worktree(_repo.FullPath)
                        .Use(log)
                        .PruneAsync();
                }
            }
            else
            {
                await DeleteRemoteBranchAsync(Target, log);
                _repo.UIStates.RemoveHistoryFilter(Target.FullName, Models.FilterType.RemoteBranch);
            }

            log.Complete();
            _repo.MarkBranchesDirtyManually();
            return true;
        }

        private async Task DeleteRemoteBranchAsync(Models.Branch branch, CommandLog log)
        {
            var exists = await new Commands.Remote(_repo.FullPath)
                .HasBranchAsync(branch.Remote, branch.Name)
                .ConfigureAwait(false);

            if (exists)
                await new Commands.Push(_repo.FullPath, branch.Remote, $"refs/heads/{branch.Name}", true)
                    .Use(log)
                    .RunAsync()
                    .ConfigureAwait(false);
            else
                await new Commands.Branch(_repo.FullPath, branch.Name)
                    .Use(log)
                    .DeleteRemoteAsync(branch.Remote)
                    .ConfigureAwait(false);
        }

        private readonly Repository _repo = null;
        private bool _alsoDeleteTrackingRemote = false;
        private bool _alsoRemoveWorktree = false;
    }
}
