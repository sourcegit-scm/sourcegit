using System.Threading.Tasks;
using System.Windows.Controls;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     本地分支改名
    /// </summary>
    public partial class RenameBranch : Controls.PopupWidget {
        private string repo = null;
        private string target = null;

        public string NewName { get; set; }

        public RenameBranch(Models.Repository repo, string target) {
            this.repo = repo.Path;
            this.target = target;

            InitializeComponent();

            ruleBranch.Repo = repo;
            txtTarget.Text = target;
        }

        public override string GetTitle() {
            return App.Text("RenameBranch");
        }

        public override Task<bool> Start() {
            txtNewName.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtNewName)) return null;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.Branch(repo, target).Rename(NewName);
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
