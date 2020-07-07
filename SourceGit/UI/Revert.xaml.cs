using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Confirm to revert selected commit.
    /// </summary>
    public partial class Revert : UserControl {
        private Git.Repository repo = null;
        private string sha = null;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="opened">Opened repository</param>
        /// <param name="commit">Commit to be reverted</param>
        public Revert(Git.Repository opened, Git.Commit commit) {
            repo = opened;
            sha = commit.SHA;

            InitializeComponent();
            txtDesc.Content = $"{commit.ShortSHA}  {commit.Subject}";
        }

        /// <summary>
        ///     Open this dialog.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="commit"></param>
        public static void Show(Git.Repository repo, Git.Commit commit) {
            PopupManager.Show(new Revert(repo, commit));
        }

        /// <summary>
        ///     Start revert.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Sure(object sender, RoutedEventArgs e) {
            bool autoCommit = chkCommit.IsChecked == true;

            PopupManager.Lock();
            await Task.Run(() => repo.Revert(sha, autoCommit));
            PopupManager.Close(true);
        }

        /// <summary>
        ///     Cancel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel(object sender, RoutedEventArgs e) {
            PopupManager.Close();
        }
    }
}
