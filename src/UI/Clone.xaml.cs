using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Clone dialog.
    /// </summary>
    public partial class Clone : UserControl {
        private PopupManager popup = null;
        private Action cb = null;

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
        ///     Remote name.
        /// </summary>
        public string RemoteName { get; set; }

        /// <summary>
        ///     Additional parameters
        /// </summary>
        public string AdditionalParam { get; set; }

        /// <summary>
        ///     Constructor.
        /// </summary>
        public Clone(PopupManager mgr, Action success) {
            ParentFolder = App.Setting.Tools.GitDefaultCloneDir;
            popup = mgr;
            cb = success;
            InitializeComponent();
        }

        /// <summary>
        ///     Select parent folder.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectParentFolder(object sender, RoutedEventArgs e) {
            FolderDialog.Open(App.Text("Clone.RemoteFolder.Placeholder"), path => {
                txtParentFolder.Text = path;
            });
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

            string rName;
            if (string.IsNullOrWhiteSpace(RemoteName)){
                rName = null;
            } else {
                rName = RemoteName;
            }

            popup.Lock();

            var succ = await Task.Run(() => {
                return Git.Repository.Clone(RemoteUri, ParentFolder, rName, repoName, AdditionalParam, popup.UpdateStatus);
            });

            if (succ) {
                var path = new DirectoryInfo(ParentFolder + "/" + repoName).FullName;
                var repo = App.Setting.AddRepository(path, "");
                App.Open(repo);
                cb?.Invoke();
                popup.Close(true);
            } else {
                popup.Unlock();
            }
        }

        /// <summary>
        ///     Cancel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel(object sender, RoutedEventArgs e) {
            popup.Close();
        }
    }
}
