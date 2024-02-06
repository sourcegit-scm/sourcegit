using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class DeleteRemote : Popup {
        public Models.Remote Remote {
            get;
            private set;
        }

        public DeleteRemote(Repository repo, Models.Remote remote) {
            _repo = repo;
            Remote = remote;
            View = new Views.DeleteRemote() { DataContext = this };
        }

        public override Task<bool> Sure() {
            _repo.SetWatcherEnabled(false);
            return Task.Run(() => {
                SetProgressDescription("Deleting remote ...");
                var succ = new Commands.Remote(_repo.FullPath).Delete(Remote.Name);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private Repository _repo = null;
    }
}
