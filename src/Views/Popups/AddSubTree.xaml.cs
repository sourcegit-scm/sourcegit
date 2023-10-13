using System.Threading.Tasks;
using System.Windows.Controls;

namespace SourceGit.Views.Popups {

    /// <summary>
    ///     添加子树面板
    /// </summary>
    public partial class AddSubTree : Controls.PopupWidget {
        private Models.Repository repo = null;

        public string Source { get; set; }
        public string Ref { get; set; }
        public string Prefix { get; set; }

        public AddSubTree(Models.Repository repo) {
            this.repo = repo;
            InitializeComponent();
        }

        public override string GetTitle() {
            return App.Text("AddSubTree");
        }

        public override Task<bool> Start() {
            txtSource.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtSource)) return null;

            txtPrefix.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtPrefix)) return null;

            txtRef.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtRef)) return null;

            var squash = chkSquash.IsChecked == true;
            if (repo.SubTrees.FindIndex(x => x.Prefix == Prefix) >= 0) {
                App.Exception(repo.Path, $"Subtree add failed. Prefix({Prefix}) already exists!");
                return null;
            }

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo.Path, false);
                var succ = new Commands.SubTree(repo.Path).Add(Prefix, Source, Ref, squash, UpdateProgress);
                if (succ) {
                    repo.SubTrees.Add(new Models.SubTree() {
                        Prefix = Prefix,
                        Remote = Source,
                    });
                    Models.Preference.Save();
                    Models.Watcher.Get(repo.Path)?.RefreshSubTrees();
                }
                Models.Watcher.SetEnabled(repo.Path, true);
                return succ;
            });
        }
    }
}
