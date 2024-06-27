using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class PruneWorktrees : Popup
    {
        public PruneWorktrees(Repository repo)
        {
            _repo = repo;
            View = new Views.PruneWorktrees() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Prune worktrees ...";

            return Task.Run(() =>
            {
                new Commands.Worktree(_repo.FullPath).Prune(SetProgressDescription);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private Repository _repo = null;
    }
}
