using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class LFSPull : Popup
    {
        public LFSPull(Repository repo)
        {
            _repo = repo;
            View = new Views.LFSPull() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Pull LFS objects from remote ...";
            return Task.Run(() =>
            {
                new Commands.LFS(_repo.FullPath).Pull(SetProgressDescription);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private readonly Repository _repo = null;
    }
}
