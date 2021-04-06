using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Repository configuration dialog
    /// </summary>
    public partial class Configure : UserControl {
        private Git.Repository repo = null;

        /// <summary>
        ///     User name for this repository.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        ///     User email for this repository.
        /// </summary>
        public string UserEmail { get; set; }

        /// <summary>
        ///     Commit template for this repository.
        /// </summary>
        public string CommitTemplate { get; set; }

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="repo"></param>
        public Configure(Git.Repository repo) {
            this.repo = repo;

            UserName = repo.GetConfig("user.name");
            UserEmail = repo.GetConfig("user.email");

            InitializeComponent();
        }

        /// <summary>
        ///     Show this dialog.
        /// </summary>
        /// <param name="repo"></param>
        public static void Show(Git.Repository repo) {
            repo.GetPopupManager()?.Show(new Configure(repo));
        }

        #region EVENTS
        private void Save(object sender, RoutedEventArgs e) {
            var oldUser = repo.GetConfig("user.name");
            if (oldUser != UserName) repo.SetConfig("user.name", UserName);

            var oldEmail = repo.GetConfig("user.email");
            if (oldEmail != UserEmail) repo.SetConfig("user.email", UserEmail);

            Close(sender, e);
        }

        private void Close(object sender, RoutedEventArgs e) {
            repo.GetPopupManager()?.Close();
        }
        #endregion
    }
}
