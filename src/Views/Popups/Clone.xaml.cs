using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SourceGit.Views.Popups {

    /// <summary>
    ///     克隆
    /// </summary>
    public partial class Clone : Controls.PopupWidget {

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

        public override string GetTitle() {
            return App.Text("Clone");
        }

        public override Task<bool> Start() {
            var checks = new Controls.TextEdit[] { txtUrl, txtFolder, txtLocal, txtRemote };
            foreach (var edit in checks) {
                edit.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                if (Validation.GetHasError(edit)) return null;
            }

            return Task.Run(() => {
                var extras = string.IsNullOrEmpty(ExtraArgs) ? "" : ExtraArgs;
                if (!string.IsNullOrEmpty(RemoteName)) extras += $" --origin {RemoteName}";

                var succ = new Commands.Clone(Folder, Uri, LocalName, extras, UpdateProgress).Exec();
                if (!succ) return false;

                var path = Folder;
                if (!string.IsNullOrEmpty(LocalName)) {
                    path = Path.GetFullPath(Path.Combine(path, LocalName));
                } else {
                    var name = Path.GetFileName(Uri);
                    if (name.EndsWith(".git")) name = name.Substring(0, name.Length - 4);
                    path = Path.GetFullPath(Path.Combine(path, name));
                }

                if (!Directory.Exists(path)) {
                    Models.Exception.Raise($"Folder {path} not found!");
                    return false;
                }

                var gitDir = new Commands.QueryGitDir(path).Result();
                var repo = Models.Preference.Instance.AddRepository(path, gitDir, "");
                if (repo != null) Dispatcher.Invoke(() => Models.Watcher.Open(repo));
                return true;
            });
        }

        private void OnFolderSelectorClick(object sender, System.Windows.RoutedEventArgs e) {
            var dialog = new Controls.FolderDialog();
            if (dialog.ShowDialog() == true) {
                Folder = dialog.SelectedPath;
                txtFolder.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            }
        }
    }
}
