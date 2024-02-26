using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class ClearStashes : Popup {
        public ClearStashes(Repository repo) {
            _repo = repo;
            View = new Views.ClearStashes() { DataContext = this };
        }

        public override Task<bool> Sure() {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Clear all stashes...";

            return Task.Run(() => {
                new Commands.Stash(_repo.FullPath).Clear();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private Repository _repo = null;
    }
}
