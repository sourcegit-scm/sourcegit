using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Git save stash panel.
    /// </summary>
    public partial class Stash : UserControl {
        private Git.Repository repo = null;
        private List<string> files = new List<string>();

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="repo">Opened repsitory</param>
        public Stash(Git.Repository repo, List<string> files) {
            this.repo = repo;
            this.files = files;
            InitializeComponent();
            chkIncludeUntracked.IsEnabled = files.Count == 0;
        }

        /// <summary>
        ///     Open this dialog.
        /// </summary>
        /// <param name="repo">Opened repository</param>
        /// <param name="files">Special files to stash</param>
        public static void Show(Git.Repository repo, List<string> files) {
            var popup = App.Launcher.GetPopupManager(repo);
            popup?.Show(new Stash(repo, files));
        }

        /// <summary>
        ///     Start saving stash.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Start(object sender, RoutedEventArgs e) {
            bool includeUntracked = chkIncludeUntracked.IsChecked == true;
            string message = txtName.Text;

            Git.Stash.Push(repo, includeUntracked, message, files);
            App.Launcher.GetPopupManager(repo)?.Close();
        }

        /// <summary>
        ///     Cancel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel(object sender, RoutedEventArgs e) {
            App.Launcher.GetPopupManager(repo)?.Close();
        }
    }
}
