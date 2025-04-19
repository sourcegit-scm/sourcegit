using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class ClearStashes : Popup
    {
        public ClearStashes(Repository repo)
        {
            _repo = repo;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Clear all stashes...";

            var log = _repo.CreateLog("Clear Stashes");
            Use(log);

            return Task.Run(() =>
            {
                new Commands.Stash(_repo.FullPath).Use(log).Clear();
                log.Complete();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private readonly Repository _repo = null;
    }
}
