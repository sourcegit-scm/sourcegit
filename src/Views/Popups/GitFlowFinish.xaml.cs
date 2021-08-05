using System.Threading.Tasks;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     完成GitFlow分支开发
    /// </summary>
    public partial class GitFlowFinish : Controls.PopupWidget {
        private string repo = null;
        private string name = null;
        private Models.GitFlowBranchType type = Models.GitFlowBranchType.None;

        public GitFlowFinish(Models.Repository repo, string branch, Models.GitFlowBranchType type) {
            this.repo = repo.Path;
            this.type = type;

            InitializeComponent();

            txtName.Text = branch;
            switch (type) {
            case Models.GitFlowBranchType.Feature:
                txtPrefix.Text = App.Text("GitFlow.Feature");
                name = branch.Substring(repo.GitFlow.Feature.Length);
                break;
            case Models.GitFlowBranchType.Release:
                txtPrefix.Text = App.Text("GitFlow.Release");
                name = branch.Substring(repo.GitFlow.Release.Length);
                break;
            case Models.GitFlowBranchType.Hotfix:
                txtPrefix.Text = App.Text("GitFlow.Hotfix");
                name = branch.Substring(repo.GitFlow.Hotfix.Length);
                break;
            }
        }

        public override string GetTitle() {
            switch (type) {
            case Models.GitFlowBranchType.Feature:
                return App.Text("GitFlow.FinishFeature");
            case Models.GitFlowBranchType.Release:
                return App.Text("GitFlow.FinishRelease");
            case Models.GitFlowBranchType.Hotfix:
                return App.Text("GitFlow.FinishHotfix");
            default:
                return "";
            }
        }

        public override Task<bool> Start() {
            var keepBranch = chkKeep.IsChecked == true;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.GitFlow(repo).Finish(type, name, keepBranch);
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
