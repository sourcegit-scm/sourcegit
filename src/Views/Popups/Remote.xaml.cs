using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     远程信息编辑面板
    /// </summary>
    public partial class Remote : Controls.PopupWidget {
        private Models.Repository repo = null;
        private Models.Remote remote = null;

        public string RemoteName { get; set; }
        public string RemoteURL { get; set; }

        public Remote(Models.Repository repo, Models.Remote remote) {
            this.repo = repo;
            this.remote = remote;

            if (remote != null) {
                RemoteName = remote.Name;
                RemoteURL = remote.URL;
            }

            InitializeComponent();

            ruleName.Repo = repo;
            if (RemoteURL.StartsWith("git@")) {
                txtSSHKey.Text = new Commands.Config(repo.Path).Get($"remote.{remote.Name}.sshkey");
            } else {
                txtSSHKey.Text = "";
            }
        }

        public override string GetTitle() {
            return App.Text(remote == null ? "Remote.AddTitle" : "Remote.EditTitle");
        }

        public override Task<bool> Start() {
            if (remote == null || remote.Name != RemoteName) {
                txtName.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                if (Validation.GetHasError(txtName)) return null;
            }

            txtUrl.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtUrl)) return null;

            var sshKey = txtSSHKey.Text;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo.Path, false);
                if (remote == null) {
                    var succ = new Commands.Remote(repo.Path).Add(RemoteName, RemoteURL);
                    if (succ) new Commands.Fetch(repo.Path, RemoteName, true, UpdateProgress).Exec();

                    if (!string.IsNullOrEmpty(sshKey)) {
                        new Commands.Config(repo.Path).Set($"remote.{RemoteName}.sshkey", sshKey);
                    }
                } else {
                    if (remote.URL != RemoteURL) {
                        var succ = new Commands.Remote(repo.Path).SetURL(remote.Name, RemoteURL);
                        if (succ) remote.URL = RemoteURL;
                    }

                    if (remote.Name != RemoteName) {
                        var succ = new Commands.Remote(repo.Path).Rename(remote.Name, RemoteName);
                        if (succ) remote.Name = RemoteName;
                    }

                    if (!string.IsNullOrEmpty(sshKey)) {
                        new Commands.Config(repo.Path).Set($"remote.{RemoteName}.sshkey", sshKey);
                    }
                }
                Models.Watcher.SetEnabled(repo.Path, true);
                return true;
            });
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
            if (!string.IsNullOrEmpty(txtUrl.Text)) {
                rowSSHKey.Height = new GridLength(txtUrl.Text.StartsWith("git@") ? 32 : 0, GridUnitType.Pixel);
            } else {
                rowSSHKey.Height = new GridLength(0, GridUnitType.Pixel);
            }
        }
    }
}
