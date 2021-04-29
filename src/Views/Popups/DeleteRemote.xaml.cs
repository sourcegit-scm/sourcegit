using System.Threading.Tasks;

namespace SourceGit.Views.Popups {

    /// <summary>
    ///     删除远程确认
    /// </summary>
    public partial class DeleteRemote : Controls.PopupWidget {
        private string repo = null;
        private string remote = null;

        public DeleteRemote(string repo, string remote) {
            this.repo = repo;
            this.remote = remote;

            InitializeComponent();
            txtTarget.Text = remote;
        }

        public override string GetTitle() {
            return App.Text("DeleteRemote");
        }

        public override Task<bool> Start() {
            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.Remote(repo).Delete(remote);
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
