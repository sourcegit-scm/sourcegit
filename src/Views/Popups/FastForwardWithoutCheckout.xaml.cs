using System.Threading.Tasks;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     对于不是当前分支的本地分支，Fast-Forward
    /// </summary>
    public partial class FastForwardWithoutCheckout : Controls.PopupWidget {
        private string repo = null;
        private string remote = null;
        private string localBranch = null;
        private string remoteBranch = null;
        private bool isValid = false;

        public FastForwardWithoutCheckout(string repo, string branch, string upstream) {
            int idx = upstream.IndexOf('/');
            if (idx < 0 || idx == upstream.Length - 1) {
                App.Exception(repo, $"Invalid upstream: {upstream}");
                return;
            }

            this.repo = repo;
            this.remote = upstream.Substring(0, idx);
            this.localBranch = branch;
            this.remoteBranch = upstream.Substring(idx+1);
            this.isValid = true;

            InitializeComponent();
        }

        public override string GetTitle() {
            return App.Text("Fetch.Title");
        }

        public override Task<bool> Start() {
            return Task.Run(() => {
                if (isValid) {
                    Models.Watcher.SetEnabled(repo, false);
                    new Commands.Fetch(repo, remote, localBranch, remoteBranch, UpdateProgress).Exec();
                    Models.Watcher.SetEnabled(repo, true);
                }
                
                return true;
            });
        }
    }
}
