using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Dialog to initialize git flow.
    /// </summary>
    public partial class GitFlowSetup : UserControl {
        private Git.Repository repo = null;
        private Regex regex = new Regex(@"^[\w\-/\.]+$");

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="opened"></param>
        public GitFlowSetup(Git.Repository opened) {
            repo = opened;
            InitializeComponent();
        }

        /// <summary>
        ///     Open this dialog.
        /// </summary>
        /// <param name="repo"></param>
        public static void Show(Git.Repository repo) {
            repo.GetPopupManager()?.Show(new GitFlowSetup(repo));
        }

        /// <summary>
        ///     Start to initialize git-flow.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Sure(object sender, RoutedEventArgs e) {
            var popup = repo.GetPopupManager();
            popup?.Lock();

            var master = txtMaster.Text;
            var dev = txtDevelop.Text;
            var feature = txtFeature.Text;
            var release = txtRelease.Text;
            var hotfix = txtHotfix.Text;
            var version = txtVersion.Text;

            await Task.Run(() => repo.EnableGitFlow(master, dev, feature, release, hotfix, version));
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

        /// <summary>
        ///     Validate input names.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ValidateNames(object sender, TextChangedEventArgs e) {
            if (!IsLoaded) return;

            var master = txtMaster.Text;
            var dev = txtDevelop.Text;
            var feature = txtFeature.Text;
            var release = txtRelease.Text;
            var hotfix = txtHotfix.Text;

            if (!ValidateBranch("Production", master)) return;
            if (!ValidateBranch("Development", dev)) return;

            if (dev == master) {
                txtValidation.Content = App.Text("GitFlow.DevSameAsProd");
                btnSure.IsEnabled = false;
                return;
            }

            if (!ValidatePrefix("Feature", feature)) return;
            if (!ValidatePrefix("Release", release)) return;
            if (!ValidatePrefix("Hotfix", hotfix)) return;

            txtValidation.Content = "";
            btnSure.IsEnabled = true;
        }

        private bool ValidateBranch(string type, string name) {
            if (string.IsNullOrEmpty(name)) {
                txtValidation.Content = App.Format("GitFlow.BranchRequired", type);
                btnSure.IsEnabled = false;
                return false;
            }

            if (!regex.IsMatch(name)) {
                txtValidation.Content = App.Format("GitFlow.BranchInvalid", type);
                btnSure.IsEnabled = false;
                return false;
            }

            return true;
        }

        private bool ValidatePrefix(string type, string prefix) {
            if (string.IsNullOrEmpty(prefix)) {
                txtValidation.Content = App.Format("GitFlow.PrefixRequired", type);
                btnSure.IsEnabled = false;
                return false;
            }

            if (!regex.IsMatch(prefix)) {
                txtValidation.Content = App.Format("GitFlow.PrefixInvalid", type);
                btnSure.IsEnabled = false;
                return false;
            }

            return true;
        }
    }
}
