using System.Threading.Tasks;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     初始化Git-Flow
    /// </summary>
    public partial class InitGitFlow : Controls.PopupWidget {
        private Models.Repository repo = null;

        public InitGitFlow(Models.Repository repo) {
            this.repo = repo;
            InitializeComponent();
        }

        public override string GetTitle() {
            return App.Text("GitFlow.Init");
        }

        public override Task<bool> Start() {
            var master = txtMaster.Text;
            var dev = txtDevelop.Text;
            var feature = txtFeature.Text;
            var release = txtRelease.Text;
            var hotfix = txtHotfix.Text;
            var version = txtTag.Text;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo.Path, false);
                var succ = new Commands.GitFlow(repo.Path).Init(master, dev, feature, release, hotfix, version);
                var cmd = new Commands.Config(repo.Path);
                if (succ) {
                    repo.GitFlow.Feature = cmd.Get("gitflow.prefix.feature");
                    repo.GitFlow.Release = cmd.Get("gitflow.prefix.release");
                    repo.GitFlow.Hotfix = cmd.Get("gitflow.prefix.hotfix");
                } else {
                    cmd.Set("gitflow.branch.master", null);
                    cmd.Set("gitflow.branch.develop", null);
                    cmd.Set("gitflow.prefix.feature", null);
                    cmd.Set("gitflow.prefix.bugfix", null);
                    cmd.Set("gitflow.prefix.release", null);
                    cmd.Set("gitflow.prefix.hotfix", null);
                    cmd.Set("gitflow.prefix.support", null);
                    cmd.Set("gitflow.prefix.versiontag", null);
                }
                Models.Watcher.SetEnabled(repo.Path, true);
                return true;
            });
        }
    }
}
