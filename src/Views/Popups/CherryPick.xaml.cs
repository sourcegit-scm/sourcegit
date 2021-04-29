using System.Threading.Tasks;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     遴选面板
    /// </summary>
    public partial class CherryPick : Controls.PopupWidget {
        private string repo = null;
        private string commit = null;

        public CherryPick(string repo, Models.Commit commit) {
            this.repo = repo;
            this.commit = commit.SHA;

            InitializeComponent();

            txtCommit.Text = $"{commit.ShortSHA}  {commit.Subject}";
        }

        public override string GetTitle() {
            return App.Text("CherryPick.Title");
        }

        public override Task<bool> Start() {
            var noCommits = chkCommit.IsChecked != true;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.CherryPick(repo, commit, noCommits).Exec();
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
