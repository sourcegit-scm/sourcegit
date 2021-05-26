using System.Threading.Tasks;

namespace SourceGit.Views.Popups {

    /// <summary>
    ///     切换分支
    /// </summary>
    public partial class Checkout : Controls.PopupWidget {
        private string repo;
        private string branch;

        public Checkout(string repo, string branch) {
            this.repo = repo;
            this.branch = branch;

            InitializeComponent();
        }

        public override string GetTitle() {
            return App.Text("BranchCM.Checkout", branch);
        }

        public override Task<bool> Start() {
            UpdateProgress(GetTitle());

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.Checkout(repo).Branch(branch, UpdateProgress);
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
