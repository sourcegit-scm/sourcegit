using System.Threading.Tasks;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     删除子树
    /// </summary>
    public partial class UnlinkSubTree : Controls.PopupWidget {
        private Models.Repository repo;
        private string prefix;

        public UnlinkSubTree(Models.Repository repo, string prefix) {
            this.repo = repo;
            this.prefix = prefix;
            InitializeComponent();
            txtPrefix.Text = prefix;
        }

        public override string GetTitle() {
            return App.Text("UnlinkSubTree");
        }

        public override Task<bool> Start() {
            return Task.Run(() => {
                var idx = repo.SubTrees.FindIndex(x => x.Prefix == prefix);
                if (idx >= 0) {
                    repo.SubTrees.RemoveAt(idx);
                    Models.Preference.Save();
                    Models.Watcher.Get(repo.Path)?.RefreshSubTrees();
                }
                return true;
            });
        }
    }
}
