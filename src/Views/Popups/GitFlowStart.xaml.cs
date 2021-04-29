using System.Threading.Tasks;
using System.Windows.Controls;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     Git-Flow start命令操作面板
    /// </summary>
    public partial class GitFlowStart : Controls.PopupWidget {
        private string repo = null;
        private Models.GitFlowBranchType type = Models.GitFlowBranchType.None;

        public string BranchName { get; set; }

        public GitFlowStart(Models.Repository repo, Models.GitFlowBranchType type) {
            this.repo = repo.Path;
            this.type = type;

            InitializeComponent();

            ruleBranch.Repo = repo;
            switch (type) {
            case Models.GitFlowBranchType.Feature:
                ruleBranch.Prefix = repo.GitFlow.Feature;
                txtPrefix.Text = repo.GitFlow.Feature;
                break;
            case Models.GitFlowBranchType.Release:
                ruleBranch.Prefix = repo.GitFlow.Release;
                txtPrefix.Text = repo.GitFlow.Release;
                break;
            case Models.GitFlowBranchType.Hotfix:
                ruleBranch.Prefix = repo.GitFlow.Hotfix;
                txtPrefix.Text = repo.GitFlow.Hotfix;
                break;
            }
        }

        public override string GetTitle() {
            switch (type) {
            case Models.GitFlowBranchType.Feature:
                return App.Text("GitFlow.StartFeatureTitle");
            case Models.GitFlowBranchType.Release:
                return App.Text("GitFlow.StartReleaseTitle");
            case Models.GitFlowBranchType.Hotfix:
                return App.Text("GitFlow.StartHotfixTitle");
            default:
                return "";
            }
        }

        public override Task<bool> Start() {
            txtBranchName.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtBranchName)) return null;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.GitFlow(repo).Start(type, BranchName);
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
