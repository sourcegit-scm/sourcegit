using System.Threading.Tasks;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     删除分支确认
    /// </summary>
    public partial class DeleteBranch : Controls.PopupWidget {
        private string repo = null;
        private string branch = null;
        private string remote = null;

        public DeleteBranch(string repo, string branch, string remote = null) {
            this.repo = repo;
            this.branch = branch;
            this.remote = remote;

            InitializeComponent();

            if (string.IsNullOrEmpty(remote)) txtTarget.Text = branch;
            else txtTarget.Text = $"{remote}/{branch}";
        }

        public override string GetTitle() {
            return App.Text("DeleteBranch");
        }

        public override Task<bool> Start() {
            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                if (string.IsNullOrEmpty(remote)) {
                    new Commands.Branch(repo, branch).Delete();
                } else {
                    new Commands.Push(repo, remote, branch).Exec();
                }
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
