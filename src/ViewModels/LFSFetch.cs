using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class LFSFetch : Popup
    {
        public LFSFetch(Repository repo)
        {
            _repo = repo;
            View = new Views.LFSFetch() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Fetching LFS objects from remote ...";
            return Task.Run(() =>
            {
                new Commands.LFS(_repo.FullPath).Fetch(SetProgressDescription);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private readonly Repository _repo = null;
    }
}
