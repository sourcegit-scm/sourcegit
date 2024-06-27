using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class LockWorktree : Popup
    {
        public Models.Worktree Target
        {
            get;
            private set;
        } = null;

        public string Reason
        {
            get;
            set;
        } = string.Empty;

        public LockWorktree(Repository repo, Models.Worktree target)
        {
            _repo = repo;
            Target = target;
            View = new Views.LockWorktree() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Locking worktrees ...";

            return Task.Run(() =>
            {
                var succ = new Commands.Worktree(_repo.FullPath).Lock(Target.FullPath, Reason);
                if (succ)
                    Target.IsLocked = true;

                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private Repository _repo = null;
    }
}
