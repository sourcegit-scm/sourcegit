using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Delete tag dialog.
    /// </summary>
    public partial class DeleteTag : UserControl {
        private Git.Repository repo = null;
        private Git.Tag tag = null;

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="repo">Opened repo</param>
        /// <param name="tag">Delete tag</param>
        public DeleteTag(Git.Repository repo, Git.Tag tag) {
            this.repo = repo;
            this.tag = tag;

            InitializeComponent();
            tagName.Text = tag.Name;
        }

        /// <summary>
        ///     Display this dialog.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="tag"></param>
        public static void Show(Git.Repository repo, Git.Tag tag) {
            repo.GetPopupManager()?.Show(new DeleteTag(repo, tag));
        }

        /// <summary>
        ///     Start request.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Start(object sender, RoutedEventArgs e) {
            var popup = repo.GetPopupManager();
            popup?.Lock();

            var push = chkWithRemote.IsChecked == true;
            await Task.Run(() => Git.Tag.Delete(repo, tag.Name, push));

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
