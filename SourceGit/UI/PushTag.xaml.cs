using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Push tag to remote dialog
    /// </summary>
    public partial class PushTag : UserControl {
        private Git.Repository repo = null;
        private Git.Tag tag = null;

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="repo">Opened repo</param>
        /// <param name="tag">Delete tag</param>
        public PushTag(Git.Repository repo, Git.Tag tag) {
            this.repo = repo;
            this.tag = tag;

            InitializeComponent();
            tagName.Content = tag.Name;
            combRemotes.ItemsSource = repo.Remotes();
            combRemotes.SelectedIndex = 0;
        }

        /// <summary>
        ///     Display this dialog.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="tag"></param>
        public static void Show(Git.Repository repo, Git.Tag tag) {
            var popup = App.Launcher.GetPopupManager(repo);
            popup?.Show(new PushTag(repo, tag));
        }

        /// <summary>
        ///     Start request.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Start(object sender, RoutedEventArgs e) {
            var remote = combRemotes.SelectedItem as Git.Remote;
            if (remote == null) return;

            var popup = App.Launcher.GetPopupManager(repo);
            popup?.Lock();
            await Task.Run(() => Git.Tag.Push(repo, tag.Name, remote.Name));
            popup?.Close(true);
        }

        /// <summary>
        ///     Cancel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel(object sender, RoutedEventArgs e) {
            var popup = App.Launcher.GetPopupManager(repo);
            popup?.Close();
        }
    }
}
