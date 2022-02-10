using System.Threading.Tasks;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     清理仓库
    /// </summary>
    public partial class Cleanup : Controls.PopupWidget {
        private string repo;

        public Cleanup(string repo) {
            this.repo = repo;
            InitializeComponent();
        }

        public override string GetTitle() {
            return App.Text("Dashboard.Clean");
        }

        public override Task<bool> Start() {
            UpdateProgress(GetTitle());

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.GC(repo, UpdateProgress).Exec();

                var lfs = new Commands.LFS(repo);
                if (lfs.IsEnabled()) lfs.Prune(UpdateProgress);

                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
