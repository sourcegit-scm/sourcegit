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
                    path += $"/{LocalName}";
                } else {
                    var idx = Uri.LastIndexOfAny(new char[] { '\\', '/' });
                    var name = Uri.Substring(idx + 1);
                    path += $"/{name.Replace(".git", "")}";
                }

                var repo = Models.Preference.Instance.AddRepository(path, path + "/.git", "");
                if (repo != null) Dispatcher.Invoke(() => Models.Watcher.Open(repo));
                return true;
            });
        }

        private void OnFolderSelectorClick(object sender, System.Windows.RoutedEventArgs e) {
            FolderBrowser.Open(null, App.Text("Clone.Folder.Placeholder"), path => {
                Folder = path;
                txtFolder.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            });
        }
    }
}
