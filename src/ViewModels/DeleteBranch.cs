﻿using System.Threading.Tasks;

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
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Deleting branch...";

            var log = _repo.CreateLog("Delete Branch");
            Use(log);

            if (Target.IsLocal)
            {
                await new Commands.Branch(_repo.FullPath, Target.Name)
                    .Use(log)
                    .DeleteLocalAsync();

                if (_alsoDeleteTrackingRemote && TrackingRemoteBranch != null)
                    await DeleteRemoteBranchAsync(TrackingRemoteBranch, log);
            }
            else
            {
                await DeleteRemoteBranchAsync(Target, log);
            }

            log.Complete();
            _repo.MarkBranchesDirtyManually();
            _repo.SetWatcherEnabled(true);
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

        private readonly Repository _repo;
        private bool _alsoDeleteTrackingRemote;
    }
}
