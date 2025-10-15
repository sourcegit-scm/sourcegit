using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class LFSPush : Popup
    {
        public List<Models.Remote> Remotes => _repo.Remotes;

        public Models.Remote SelectedRemote
        {
            get;
            set;
        }

        public LFSPush(Repository repo)
        {
            _repo = repo;
            SelectedRemote = _repo.Remotes[0];
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Push LFS objects to remote ...";

            var log = _repo.CreateLog("LFS Push");
            Use(log);

            await new Commands.LFS(_repo.FullPath)
                .Use(log)
                .PushAsync(SelectedRemote.Name);

            log.Complete();
            return true;
        }

        private readonly Repository _repo = null;
    }
}
