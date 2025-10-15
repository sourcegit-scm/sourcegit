using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class LFSPrune : Popup
    {
        public LFSPrune(Repository repo)
        {
            _repo = repo;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "LFS prune ...";

            var log = _repo.CreateLog("LFS Prune");
            Use(log);

            await new Commands.LFS(_repo.FullPath)
                .Use(log)
                .PruneAsync();

            log.Complete();
            return true;
        }

        private readonly Repository _repo = null;
    }
}
