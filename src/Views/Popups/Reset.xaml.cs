using System.Threading.Tasks;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     重置面板
    /// </summary>
    public partial class Reset : Controls.PopupWidget {
        private string repo = null;
        private string revision = null;

        public Reset(string repo, string current, Models.Commit to) {
            this.repo = repo;
            this.revision = to.SHA;

            InitializeComponent();

            txtCurrent.Text = current;
            txtSHA.Text = to.ShortSHA;
            txtMoveTo.Text = to.Subject;
        }

        public override string GetTitle() {
            return App.Text("Reset");
        }

        public override Task<bool> Start() {
            var mode = cmbMode.SelectedItem as Models.ResetMode;
            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.Reset(repo, revision, mode.Arg).Exec();
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
