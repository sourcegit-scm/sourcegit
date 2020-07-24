using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Fetch dialog.
    /// </summary>
    public partial class Fetch : UserControl {
        private Git.Repository repo = null;

        /// <summary>
        ///     Constructor.    
        /// </summary>
        /// <param name="opened">Opened repository</param>
        /// <param name="preferRemote">Prefer selected remote.</param>
        public Fetch(Git.Repository opened, string preferRemote) {
            repo = opened;
            InitializeComponent();

            Task.Run(() => {
                var remotes = repo.Remotes();
                Dispatcher.Invoke(() => {
                    combRemotes.ItemsSource = remotes;
                    if (preferRemote != null) {
                        combRemotes.SelectedIndex = remotes.FindIndex(r => r.Name == preferRemote);
                        chkFetchAll.IsChecked = false;
                    } else {
                        combRemotes.SelectedIndex = 0;
                        chkFetchAll.IsChecked = true;
                    }
                });
            });
        }

        /// <summary>
        ///     Show fetch dialog.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="preferRemote"></param>
        public static void Show(Git.Repository repo, string preferRemote = null) {
            PopupManager.Show(new Fetch(repo, preferRemote));
        }

        /// <summary>
        ///     Start fetch
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Start(object sender, RoutedEventArgs e) {
            bool prune = chkPrune.IsChecked == true;

            string subMod;
            if (RbtnRrsSubModYes.IsChecked == true) {
                subMod = "--recurse-submodules=yes";
            } else if (RbtnRrsSubModNo.IsChecked == true) {
                subMod = "--recurse-submodules=no";
            } else  {
                subMod = "--recurse-submodules=on-demand";
            }

            PopupManager.Lock();

            if (chkFetchAll.IsChecked == true) {
                await Task.Run(() => repo.Fetch(null, subMod, prune, PopupManager.UpdateStatus));
            } else {
                var remote = combRemotes.SelectedItem as Git.Remote;
                await Task.Run(() => repo.Fetch(remote, subMod, prune, PopupManager.UpdateStatus));
            }

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
