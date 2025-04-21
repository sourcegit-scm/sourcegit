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

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Deleting branch...";

            var log = _repo.CreateLog("Delete Branch");
            Use(log);

            return Task.Run(() =>
            {
                if (Target.IsLocal)
                {
                    Commands.Branch.DeleteLocal(_repo.FullPath, Target.Name, log);

                    if (_alsoDeleteTrackingRemote && TrackingRemoteBranch != null)
                        Commands.Branch.DeleteRemote(_repo.FullPath, TrackingRemoteBranch.Remote, TrackingRemoteBranch.Name, log);
                }
                else
                {
                    Commands.Branch.DeleteRemote(_repo.FullPath, Target.Remote, Target.Name, log);
                }

                log.Complete();

                CallUIThread(() =>
                {
                    _repo.MarkBranchesDirtyManually();
                    _repo.SetWatcherEnabled(true);
                });
                return true;
            });
        }

        private readonly Repository _repo = null;
        private bool _alsoDeleteTrackingRemote = false;
    }
}
