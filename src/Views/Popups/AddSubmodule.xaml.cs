using System.Threading.Tasks;
using System.Windows.Controls;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     新增子模块面板
    /// </summary>
    public partial class AddSubmodule : Controls.PopupWidget {
        private string repo = null;

        public string URL { get; set; }
        public string Path { get; set; }

        public AddSubmodule(string repo) {
            this.repo = repo;
            InitializeComponent();
        }

        public override string GetTitle() {
            return App.Text("Submodule.Add");
        }

        public override Task<bool> Start() {
            txtURL.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtURL)) return null;

            txtPath.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtPath)) return null;

            var recursive = chkNested.IsChecked == true;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                var succ = new Commands.Submodule(repo).Add(URL, Path, recursive, UpdateProgress);
                Models.Watcher.SetEnabled(repo, true);
                return succ;
            });
        }
    }
}
