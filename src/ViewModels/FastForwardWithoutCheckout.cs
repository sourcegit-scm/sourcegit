using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class FastForwardWithoutCheckout : Popup {
        public Models.Branch Local {
            get;
            private set;
        }

        public Models.Branch To {
            get;
            private set;
        }

        public FastForwardWithoutCheckout(Repository repo, Models.Branch local, Models.Branch upstream) {
            _repo = repo;
            Local = local;
            To = upstream;
            View = new Views.FastForwardWithoutCheckout() { DataContext = this };
        }

        public override Task<bool> Sure() {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Fast-Forward ...";

            return Task.Run(() => {
                new Commands.Fetch(_repo.FullPath, To.Remote, Local.Name, To.Name, SetProgressDescription).Exec();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private Repository _repo = null;
    }
}
