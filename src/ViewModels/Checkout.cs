using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class Checkout : Popup {
        public string Branch {
            get;
            private set;
        }

        public Checkout(Repository repo, string branch) {
            _repo = repo;
            Branch = branch;
            View = new Views.Checkout() { DataContext = this };
        }

        public override Task<bool> Sure() {
            _repo.SetWatcherEnabled(false);
            return Task.Run(() => {
                SetProgressDescription($"Checkout '{Branch}' ...");
                var succ = new Commands.Checkout(_repo.FullPath).Branch(Branch, SetProgressDescription);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private Repository _repo;
    }
}
