using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class Rebase : Popup {
        public Models.Branch Current {
            get;
            private set;
        }

        public object On {
            get;
            private set;
        }

        public bool AutoStash {
            get;
            set;
        }

        public Rebase(Repository repo, Models.Branch current, Models.Branch on) {
            _repo = repo;
            _revision = on.Head;
            Current = current;
            On = on;
            AutoStash = true;
            View = new Views.Rebase() { DataContext = this };
        }

        public Rebase(Repository repo, Models.Branch current, Models.Commit on) {
            _repo = repo;
            _revision = on.SHA;
            Current = current;
            On = on;
            AutoStash = true;
            View = new Views.Rebase() { DataContext = this };
        }

        public override Task<bool> Sure() {
            _repo.SetWatcherEnabled(false);
            return Task.Run(() => {
                var succ = new Commands.Rebase(_repo.FullPath, _revision, AutoStash).Exec();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private Repository _repo = null;
        private string _revision = string.Empty;
    }
}
