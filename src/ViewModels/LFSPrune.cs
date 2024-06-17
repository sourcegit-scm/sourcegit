using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class LFSPrune : Popup
    {
        public LFSPrune(Repository repo)
        {
            _repo = repo;
            View = new Views.LFSPrune() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "LFS prune ...";

            return Task.Run(() =>
            {
                new Commands.LFS(_repo.FullPath).Prune(SetProgressDescription);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private readonly Repository _repo = null;
    }
}
