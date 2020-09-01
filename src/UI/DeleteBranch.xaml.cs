using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {
    /// <summary>
    ///     Confirm to delete branch
    /// </summary>
    public partial class DeleteBranch : UserControl {
        private Git.Repository repo = null;
        private Git.Branch branch = null;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="opened">Opened repository.</param>
        /// <param name="target">Branch to be deleted.</param>
        public DeleteBranch(Git.Repository opened, Git.Branch target) {
            InitializeComponent();
            repo = opened;
            branch = target;
            branchName.Text = target.Name;
        }

        /// <summary>
        ///     Show this dialog.
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="branch"></param>
        public static void Show(Git.Repository opened, Git.Branch branch) {
            opened.GetPopupManager()?.Show(new DeleteBranch(opened, branch));
        }

        /// <summary>
        ///     Delete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Sure(object sender, RoutedEventArgs e) {
            var popup = repo.GetPopupManager();
            popup?.Lock();
            await Task.Run(() => branch.Delete(repo));
            popup?.Close(true);
        }

        /// <summary>
        ///     Cancel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel(object sender, RoutedEventArgs e) {
            repo.GetPopupManager()?.Close();
        }
    }
}
