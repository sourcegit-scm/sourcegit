using System.Threading.Tasks;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     清理远程已删除分支
    /// </summary>
    public partial class Prune : Controls.PopupWidget {
        private string repo = null;
        private string remote = null;

        public Prune(string repo, string remote) {
            this.repo = repo;
            this.remote = remote;
            InitializeComponent();
        }

        public override string GetTitle() {
            return App.Text("RemoteCM.Prune");
        }

        public override Task<bool> Start() {
            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                var succ = new Commands.Remote(repo).Prune(remote);
                Models.Watcher.SetEnabled(repo, true);
                return succ;
            });
        }
    }
}
