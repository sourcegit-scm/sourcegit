using System.Threading.Tasks;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     撤销面板
    /// </summary>
    public partial class Revert : Controls.PopupWidget {
        private string repo = null;
        private string commit = null;

        public Revert(string repo, Models.Commit commit) {
            this.repo = repo;
            this.commit = commit.SHA;

            InitializeComponent();

            txtSHA.Text = commit.ShortSHA;
            txtCommit.Text = commit.Subject;
        }

        public override string GetTitle() {
            return App.Text("Revert");
        }

        public override Task<bool> Start() {
            var commitChanges = chkCommit.IsChecked == true;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.Revert(repo, commit, commitChanges).Exec();
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
