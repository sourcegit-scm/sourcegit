using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Cleanup : Popup
    {
        public Cleanup(Repository repo)
        {
            _repo = repo;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Cleanup (GC & prune) ...";

            var log = _repo.CreateLog("Cleanup (GC & prune)");
            Use(log);

            await new Commands.GC(_repo.FullPath)
                .Use(log)
                .ExecAsync();

            log.Complete();
            return true;
        }

        private readonly Repository _repo = null;
    }
}
