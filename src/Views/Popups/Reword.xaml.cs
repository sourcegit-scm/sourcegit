using System.Threading.Tasks;
using System.Windows.Controls;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     编辑HEAD的提交描述
    /// </summary>
    public partial class Reword : Controls.PopupWidget {
        private string repo = null;
        private string old = null;

        public string Msg { get; set; }

        public Reword(string repo, Models.Commit commit) {
            this.repo = repo;
            this.old = $"{commit.Subject}\n{commit.Message}".Trim();
            this.Msg = old;
            InitializeComponent();

            txtSHA.Text = commit.ShortSHA;
            txtCurrent.Text = commit.Subject;
        }

        public override string GetTitle() {
            return App.Text("Reword");
        }

        public override Task<bool> Start() {
            txtMsg.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtMsg)) return null;

            return Task.Run(() => {
                if (old == Msg) return true;

                Models.Watcher.SetEnabled(repo, false);
                new Commands.Reword(repo, Msg).Exec();
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
