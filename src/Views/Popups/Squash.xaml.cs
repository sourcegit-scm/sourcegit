using System.Threading.Tasks;
using System.Windows.Controls;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     合并当前HEAD提交到上一个
    /// </summary>
    public partial class Squash : Controls.PopupWidget {
        private string repo = null;
        private string to = null;

        public string Msg { get; set; }

        public Squash(string repo, Models.Commit head, Models.Commit parent) {
            this.repo = repo;
            this.to = parent.SHA;
            this.Msg = $"{parent.Subject}\n{parent.Message}".Trim();
            InitializeComponent();
            txtHead.Text = $"{head.ShortSHA}  {head.Subject}";
            txtParent.Text = $"{parent.ShortSHA}  {parent.Subject}";
        }

        public override string GetTitle() {
            return App.Text("Squash");
        }

        public override Task<bool> Start() {
            txtMsg.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtMsg)) return null;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                var succ = new Commands.Reset(repo, to, "--soft").Exec();
                if (succ) new Commands.Commit(repo, Msg, true).Exec();
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
