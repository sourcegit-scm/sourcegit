using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;

namespace SourceGit.Views {

    /// <summary>
    ///     克隆
    /// </summary>
    public partial class Clone : Controls.Window {
        public string Uri { get; set; }
        public string Folder { get; set; }
        public string LocalName { get; set; }
        public string RemoteName { get; set; }
        public string ExtraArgs { get; set; }

        public Clone() {
            Folder = Models.Preference.Instance.Git.DefaultCloneDir;
            InitializeComponent();
            ruleRemote.IsOptional = true;
        }

        #region EVENTS
        private void OnQuit(object sender, RoutedEventArgs e) {
            Close();
        }

        private async void OnStart(object s, RoutedEventArgs e) {
            var checks = new Controls.TextEdit[] { txtUrl, txtFolder, txtLocal, txtRemote };
            foreach (var edit in checks) {
                edit.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                if (Validation.GetHasError(edit)) return;
            }

            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
            progress.Visibility = Visibility.Visible;
            processing.IsAnimating = true;

            var sshKey = txtSSHKey.Text;
            var succ = await Task.Run(() => {
                var extras = string.IsNullOrEmpty(ExtraArgs) ? "" : ExtraArgs;
                if (!string.IsNullOrEmpty(RemoteName)) extras += $" --origin {RemoteName}";

                var cloneRs = new Commands.Clone(Folder, Uri, LocalName, sshKey, extras, msg => {
                    Dispatcher.Invoke(() => txtProgress.Text = msg);
                }, err => {
                    Dispatcher.Invoke(() => txtError.Text = err);
                }).Exec();

                if (!cloneRs) return false;

                var path = Folder;
                if (!string.IsNullOrEmpty(LocalName)) {
                    path = Path.GetFullPath(Path.Combine(path, LocalName));
                } else {
                    var name = Path.GetFileName(Uri);
                    if (name.EndsWith(".git")) name = name.Substring(0, name.Length - 4);
                    path = Path.GetFullPath(Path.Combine(path, name));
                }

                if (!Directory.Exists(path)) {
                    Dispatcher.Invoke(() => txtError.Text = $"Folder {path} not found!");
                    return false;
                }

                if (!string.IsNullOrEmpty(sshKey)) {
                    var config = new Commands.Config(path);
                    var remote = "origin";
                    if (!string.IsNullOrEmpty(RemoteName)) remote = RemoteName;
                    config.Set($"remote.{remote}.sshkey", sshKey);
                }

                var gitDir = new Commands.QueryGitDir(path).Result();
                var repo = Models.Preference.Instance.AddRepository(path, gitDir);
                if (repo != null) Dispatcher.Invoke(() => Models.Watcher.Open(repo));
                return true;
            });

            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
            progress.Visibility = Visibility.Collapsed;
            processing.IsAnimating = false;
            if (succ) {
                Close();
            } else {
                exception.Visibility = Visibility.Visible;
            }
        }

        private void OnFolderSelectorClick(object sender, RoutedEventArgs e) {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.ShowNewFolderButton = true;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                Folder = dialog.SelectedPath;
                txtFolder.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            }
        }

        private void OnSelectSSHKey(object sender, RoutedEventArgs e) {
            var initPath = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "..", ".ssh"));
            if (!Directory.Exists(initPath)) Directory.CreateDirectory(initPath);

            var dialog = new OpenFileDialog();
            dialog.Filter = $"SSH Private Key|*";
            dialog.Title = App.Text("SSHKey");
            dialog.InitialDirectory = initPath;
            dialog.CheckFileExists = true;
            dialog.Multiselect = false;

            if (dialog.ShowDialog() == true) txtSSHKey.Text = dialog.FileName;
        }

        private void OnUrlChanged(object sender, TextChangedEventArgs e) {
            var isSSHProtocal = Validations.GitURL.IsSSH(txtUrl.Text);
            if (isSSHProtocal) {
                rowSSHKey.Height = new GridLength(32, GridUnitType.Pixel);
            } else {
                rowSSHKey.Height = new GridLength(0, GridUnitType.Pixel);
            }            
        }

        private void OnCloseException(object s, RoutedEventArgs e) {
            exception.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
        #endregion
    }
}
