using System.Threading.Tasks;
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
        }

        public override string GetTitle() {
            return App.Text(remote == null ? "Remote.AddTitle" : "Remote.EditTitle");
        }

        public override Task<bool> Start() {
            txtName.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtName)) return null;

            txtUrl.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtUrl)) return null;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo.Path, false);
                if (remote == null) {
                    var succ = new Commands.Remote(repo.Path).Add(RemoteName, RemoteURL);
                    if (succ) new Commands.Fetch(repo.Path, RemoteName, true, UpdateProgress).Exec();
                } else {
                    if (remote.URL != RemoteURL) {
                        new Commands.Remote(repo.Path).SetURL(remote.Name, RemoteURL);
                    }

                    if (remote.Name != RemoteName) {
                        new Commands.Remote(repo.Path).Rename(remote.Name, RemoteName);
                    }
                }
                Models.Watcher.SetEnabled(repo.Path, true);
                return true;
            });
        }
    }
}
