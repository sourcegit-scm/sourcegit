using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class PruneWorktrees : Popup
    {
        public PruneWorktrees(Repository repo)
        {
            _repo = repo;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Prune worktrees ...";

            var log = _repo.CreateLog("Prune Worktrees");
            Use(log);

            await new Commands.Worktree(_repo.FullPath)
                .Use(log)
                .PruneAsync();

            log.Complete();
            return true;
        }

        private readonly Repository _repo = null;
    }
}
