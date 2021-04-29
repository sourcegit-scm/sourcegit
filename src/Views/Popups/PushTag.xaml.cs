using System.Threading.Tasks;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     推送标签确认面板
    /// </summary>
    public partial class PushTag : Controls.PopupWidget {
        private string repo = null;
        private string tag = null;

        public PushTag(Models.Repository repo, string tag) {
            this.repo = repo.Path;
            this.tag = tag;

            InitializeComponent();

            txtTag.Text = tag;
            cmbRemotes.ItemsSource = repo.Remotes;
            cmbRemotes.SelectedIndex = 0;
        }

        public override string GetTitle() {
            return App.Text("PushTag");
        }

        public override Task<bool> Start() {
            var remote = cmbRemotes.SelectedItem as Models.Remote;
            if (remote == null) return null;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.Push(repo, remote.Name, tag, false).Exec();
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
