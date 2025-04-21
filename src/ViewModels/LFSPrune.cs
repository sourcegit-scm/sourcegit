using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class LFSPrune : Popup
    {
        public LFSPrune(Repository repo)
        {
            _repo = repo;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "LFS prune ...";

            var log = _repo.CreateLog("LFS Prune");
            Use(log);

            return Task.Run(() =>
            {
                new Commands.LFS(_repo.FullPath).Prune(log);
                log.Complete();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private readonly Repository _repo = null;
    }
}
