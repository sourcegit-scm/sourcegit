using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class LFSPull : Popup
    {
        public List<Models.Remote> Remotes => _repo.Remotes;

        public Models.Remote SelectedRemote
        {
            get;
            set;
        }

        public LFSPull(Repository repo)
        {
            _repo = repo;
            SelectedRemote = _repo.Remotes[0];
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Pull LFS objects from remote ...";

            var log = _repo.CreateLog("LFS Pull");
            Use(log);

            await new Commands.LFS(_repo.FullPath)
                .Use(log)
                .PullAsync(SelectedRemote.Name);

            log.Complete();
            return true;
        }

        private readonly Repository _repo = null;
    }
}
