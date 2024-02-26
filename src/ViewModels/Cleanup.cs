using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class Cleanup : Popup {
        public Cleanup(Repository repo) {
            _repo = repo;
            View = new Views.Cleanup() { DataContext = this };
        }

        public override Task<bool> Sure() {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Cleanup (GC & prune) ...";

            return Task.Run(() => {
                new Commands.GC(_repo.FullPath, SetProgressDescription).Exec();

                var lfs = new Commands.LFS(_repo.FullPath);
                if (lfs.IsEnabled()) {
                    SetProgressDescription("Run LFS prune ...");
                    lfs.Prune(SetProgressDescription);
                }

                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private Repository _repo = null;
    }
}
