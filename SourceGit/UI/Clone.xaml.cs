using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Clone dialog.
    /// </summary>
    public partial class Clone : UserControl {

        /// <summary>
        ///     Remote repository
        /// </summary>
        public string RemoteUri { get; set; }

        /// <summary>
        ///     Parent folder.
        /// </summary>
        public string ParentFolder { get; set; }

        /// <summary>
        ///     Local name.
        /// </summary>
        public string LocalName { get; set; }

        /// <summary>
        ///     Constructor.
        /// </summary>
        public Clone() {
            ParentFolder = App.Preference.GitDefaultCloneDir;
            InitializeComponent();
        }

        /// <summary>
        ///     Show clone dialog.
        /// </summary>
        public static void Show() {
            PopupManager.Show(new Clone());
        }

        /// <summary>
        ///     Select parent folder.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectParentFolder(object sender, RoutedEventArgs e) {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Git Repository URL";
            dialog.RootFolder = Environment.SpecialFolder.MyComputer;
            dialog.ShowNewFolderButton = true;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                txtParentFolder.Text = dialog.SelectedPath;
            }
        }

        /// <summary>
        ///     Start clone
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Start(object sender, RoutedEventArgs e) {
            txtUrl.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtUrl)) return;

            txtParentFolder.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtParentFolder)) return;

            string repoName;
            if (string.IsNullOrWhiteSpace(LocalName)) {
                var from = RemoteUri.LastIndexOfAny(new char[] { '\\', '/' });
                if (from <= 0) return;

                var name = RemoteUri.Substring(from + 1);
                repoName = name.Replace(".git", "");
            } else {
                repoName = LocalName;
            }

            PopupManager.Lock();

            var repo = await Task.Run(() => {
                return Git.Repository.Clone(RemoteUri, ParentFolder, repoName, PopupManager.UpdateStatus);
            });

            if (repo == null) {
                PopupManager.Unlock();
            } else {
                PopupManager.Close(true);
                repo.Open();
            }
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
