using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Rename branch dialog.
    /// </summary>
    public partial class RenameBranch : UserControl {
        private Git.Repository repo = null;
        private Git.Branch branch = null;

        /// <summary>
        ///     New branch name.
        /// </summary>
        public string NewName { get; set; }

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="opened">Opened repository.</param>
        /// <param name="target">Branch to rename.</param>
        public RenameBranch(Git.Repository opened, Git.Branch target) {
            repo = opened;
            branch = target;
            NewName = target.Name;

            InitializeComponent();

            nameValidator.Repo = opened;
            txtOldName.Content = target.Name;
        }

        /// <summary>
        ///     Show this dialog
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="branch"></param>
        public static void Show(Git.Repository opened, Git.Branch branch) {
            PopupManager.Show(new RenameBranch(opened, branch));
        }

        /// <summary>
        ///     Rename
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Sure(object sender, RoutedEventArgs e) {
            txtNewName.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtNewName)) return;

            PopupManager.Lock();
            await Task.Run(() => branch.Rename(repo, NewName));
            PopupManager.Close(true);
        }

        /// <summary>
        ///     Cancel merge.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel(object sender, RoutedEventArgs e) {
            PopupManager.Close();
        }
    }
}
