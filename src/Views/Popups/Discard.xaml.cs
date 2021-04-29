using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     忽略变更
    /// </summary>
    public partial class Discard : Controls.PopupWidget {
        private string repo = null;
        private List<Models.Change> changes = null;

        public Discard(string repo, List<Models.Change> changes) {
            this.repo = repo;
            this.changes = changes;

            InitializeComponent();

            if (changes == null || changes.Count == 0) {
                icon.Data = FindResource("Icon.Folder") as Geometry;
                txtTip.Text = App.Text("Discard.All");
            } else if (changes.Count == 1) {
                txtTip.Text = changes[0].Path;
            } else {
                txtTip.Text = App.Text("Discard.Total", changes.Count);
            }
        }

        public override string GetTitle() {
            return App.Text("Discard");
        }

        public override Task<bool> Start() {
            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                var cmd = new Commands.Discard(repo);
                if (changes == null || changes.Count == 0) {
                    cmd.Whole();
                } else {
                    cmd.Changes(changes);
                }
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
