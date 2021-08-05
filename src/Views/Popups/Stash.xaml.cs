using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Views.Popups {

    /// <summary>
    ///     贮藏
    /// </summary>
    public partial class Stash : Controls.PopupWidget {
        private string repo = null;
        private List<Models.Change> changes = null;

        public Stash(string repo, List<Models.Change> changes) {
            this.repo = repo;
            this.changes = changes;

            InitializeComponent();
            chkIncludeUntracked.IsEnabled = changes == null || changes.Count == 0;
        }

        public override string GetTitle() {
            return App.Text("Stash.Title");
        }

        public override Task<bool> Start() {
            var includeUntracked = chkIncludeUntracked.IsChecked == true;
            var message = txtMessage.Text;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);

                if (changes == null || changes.Count == 0) {
                    changes = new Commands.LocalChanges(repo).Result();
                }

                var jobs = new List<Models.Change>();
                foreach (var c in changes) {
                    if (c.WorkTree == Models.Change.Status.Added || c.WorkTree == Models.Change.Status.Untracked) {
                        if (includeUntracked) jobs.Add(c);
                    } else {
                        jobs.Add(c);
                    }
                }

                if (jobs.Count > 0) new Commands.Stash(repo).Push(changes, message);
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
