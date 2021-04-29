using System.Threading.Tasks;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     删除子模块面板
    /// </summary>
    public partial class DeleteSubmodule : Controls.PopupWidget {
        private string repo = null;
        private string submodule = null;

        public DeleteSubmodule(string repo, string submodule) {
            this.repo = repo;
            this.submodule = submodule;

            InitializeComponent();

            txtPath.Text = submodule;
        }

        public override string GetTitle() {
            return App.Text("DeleteSubmodule");
        }

        public override Task<bool> Start() {
            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.Submodule(repo).Delete(submodule);
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
