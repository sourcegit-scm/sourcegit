using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class StashChanges : Popup {

        public string Message {
            get;
            set;
        }

        public bool CanIgnoreUntracked {
            get;
            private set;
        }

        public bool IncludeUntracked {
            get;
            set;
        }

        public StashChanges(Repository repo, List<Models.Change> changes, bool canIgnoreUntracked) {
            _repo = repo;
            _changes = changes;
            
            CanIgnoreUntracked = canIgnoreUntracked;
            IncludeUntracked = true;
            View = new Views.StashChanges() { DataContext = this };
        }

        public override Task<bool> Sure() {
            var jobs = _changes;
            if (CanIgnoreUntracked && !IncludeUntracked) {
                jobs = new List<Models.Change>();
                foreach (var job in _changes) {
                    if (job.WorkTree != Models.ChangeState.Untracked && job.WorkTree != Models.ChangeState.Added) {
                        jobs.Add(job);
                    }
                }
            }

            if (jobs.Count == 0) return null;

            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Stash changes ...";

            return Task.Run(() => {
                new Commands.Stash(_repo.FullPath).Push(jobs, Message);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private Repository _repo = null;
        private List<Models.Change> _changes = null;
    }
}
