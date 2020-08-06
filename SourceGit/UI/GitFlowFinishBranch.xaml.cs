using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Confirm finish git-flow branch dialog
    /// </summary>
    public partial class GitFlowFinishBranch : UserControl {
        private Git.Repository repo = null;
        private Git.Branch branch = null;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="branch"></param>
        public GitFlowFinishBranch(Git.Repository repo, Git.Branch branch) {
            this.repo = repo;
            this.branch = branch;

            InitializeComponent();

            switch (branch.Kind) {
            case Git.Branch.Type.Feature:
                txtTitle.Content = "Git Flow - Finish Feature";
                txtBranchType.Content = "Feature :";
                break;
            case Git.Branch.Type.Release:
                txtTitle.Content = "Git Flow - Finish Release";
                txtBranchType.Content = "Release :";
                break;
            case Git.Branch.Type.Hotfix:
                txtTitle.Content = "Git Flow - Finish Hotfix";
                txtBranchType.Content = "Hotfix :";
                break;
            default:
                repo.GetPopupManager()?.Close();
                return;
            }

            txtBranchName.Content = branch.Name;
        }

        /// <summary>
        ///     Show this dialog.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="branch"></param>
        public static void Show(Git.Repository repo, Git.Branch branch) {
            repo.GetPopupManager()?.Show(new GitFlowFinishBranch(repo, branch));
        }

        /// <summary>
        ///     Do finish
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Sure(object sender, RoutedEventArgs e) {
            var popup = repo.GetPopupManager();
            popup?.Lock();
            await Task.Run(() => repo.FinishGitFlowBranch(branch));
            popup?.Close(true);
        }

        /// <summary>
        ///     Cancel finish
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel(object sender, RoutedEventArgs e) {
            repo.GetPopupManager()?.Close();
        }
    }
}
