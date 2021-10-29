using System.Threading.Tasks;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     确认丢弃选中的贮藏
    /// </summary>
    public partial class StashDropConfirm : Controls.PopupWidget {
        private string repo;
        private string stash;

        public StashDropConfirm(string repo, string stash, string msg) {
            this.repo = repo;
            this.stash = stash;

            InitializeComponent();

            txtTarget.Text = stash + " - " + msg;
        }

        public override string GetTitle() {
            return App.Text("StashDropConfirm");
        }

        public override Task<bool> Start() {
            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.Stash(repo).Drop(stash);
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
