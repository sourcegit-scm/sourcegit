using System.Threading.Tasks;
using System.Windows.Controls;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     拉取
    /// </summary>
    public partial class SubTreePull : Controls.PopupWidget {
        private string repo;
        private Models.SubTree subtree;

        public string Branch {
            get { return subtree.Branch; }
            set { subtree.Branch = value; }
        }

        public SubTreePull(string repo, Models.SubTree subtree) {
            this.repo = repo;
            this.subtree = subtree;
            InitializeComponent();
            txtPrefix.Text = subtree.Prefix;
            txtSource.Text = subtree.Remote;
        }

        public override string GetTitle() {
            return App.Text("SubTreePullOrPush.Pull");
        }

        public override Task<bool> Start() {
            txtBranch.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtBranch)) return null;

            var squash = chkSquash.IsChecked == true;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.SubTree(repo).Pull(subtree.Prefix, subtree.Remote, Branch, squash, UpdateProgress);
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
