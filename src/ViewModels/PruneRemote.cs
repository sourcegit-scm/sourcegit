using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class PruneRemote : Popup {
        public Models.Remote Remote {
            get;
            private set;
        }

        public PruneRemote(Repository repo, Models.Remote remote) {
            _repo = repo;
            Remote = remote;
            View = new Views.PruneRemote() { DataContext = this };
        }

        public override Task<bool> Sure() {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Run `prune` on remote ...";

            return Task.Run(() => {
                var succ = new Commands.Remote(_repo.FullPath).Prune(Remote.Name);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private Repository _repo = null;
    }
}
