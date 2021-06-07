using System.Threading.Tasks;
using System.Windows.Controls;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     推送
    /// </summary>
    public partial class SubTreePush : Controls.PopupWidget {
        private string repo;
        private Models.SubTree subtree;

        public string Branch {
            get { return subtree.Branch; }
            set { subtree.Branch = value; }
        }

        public SubTreePush(string repo, Models.SubTree subtree) {
            this.repo = repo;
            this.subtree = subtree;
            InitializeComponent();
            txtPrefix.Text = subtree.Prefix;
            txtSource.Text = subtree.Remote;
        }

        public override string GetTitle() {
            return App.Text("SubTreePullOrPush.Push");
        }

        public override Task<bool> Start() {
            txtBranch.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtBranch)) return null;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.SubTree(repo).Push(subtree.Prefix, subtree.Remote, Branch, UpdateProgress);
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
