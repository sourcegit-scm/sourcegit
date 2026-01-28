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
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Deleting branch...";

            var log = _repo.CreateLog("Delete Branch");
            Use(log);

            if (Target.IsLocal)
            {
                await new Commands.Branch(_repo.FullPath, Target.Name)
                    .Use(log)
                    .DeleteLocalAsync();
                _repo.HistoryFilterCollection.RemoveFilter(Target.FullName, Models.FilterType.LocalBranch);

                if (_alsoDeleteTrackingRemote && TrackingRemoteBranch != null)
                {
                    await DeleteRemoteBranchAsync(TrackingRemoteBranch, log);
                    _repo.HistoryFilterCollection.RemoveFilter(TrackingRemoteBranch.FullName, Models.FilterType.RemoteBranch);
                }
            }
            else
            {
                await DeleteRemoteBranchAsync(Target, log);
                _repo.HistoryFilterCollection.RemoveFilter(Target.FullName, Models.FilterType.RemoteBranch);
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
    }
}
