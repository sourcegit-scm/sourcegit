using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Create or edit remote dialog.
    /// </summary>
    public partial class Remote : UserControl {
        private Git.Repository repo = null;
        private Git.Remote remote = null;

        public string RemoteName { get; set; }
        public string RemoteUri { get; set; }

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="opened">Opened repository</param>
        /// <param name="editing">Editing remote</param>
        public Remote(Git.Repository opened, Git.Remote editing) {
            repo = opened;
            remote = editing;

            if (remote != null) {
                RemoteName = remote.Name;
                RemoteUri = remote.URL;
            }

            InitializeComponent();
            nameValidator.Repo = repo;

            if (remote != null) {
                title.Content = "Edit Remote";
            } else {
                title.Content = "Add New Remote";
            }
        }

        /// <summary>
        ///     Display this dialog.
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="editing"></param>
        public static void Show(Git.Repository opened, Git.Remote editing = null) {
            App.Launcher.GetPopupManager(opened)?.Show(new Remote(opened, editing));
        }

        /// <summary>
        ///     Commit request.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Sure(object sender, RoutedEventArgs e) {
            txtName.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtName)) return;

            txtUrl.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtUrl)) return;

            var popup = App.Launcher.GetPopupManager(repo);
            popup?.Lock();

            await Task.Run(() => {
                if (remote != null) {
                    remote.Edit(repo, RemoteName, RemoteUri);
                } else {
                    Git.Remote.Add(repo, RemoteName, RemoteUri);
                }
            });

            popup?.Close(true);
        }

        /// <summary>
        ///     Cancel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel(object sender, RoutedEventArgs e) {
            App.Launcher.GetPopupManager(repo)?.Close();
        }
    }
}
