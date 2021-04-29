using System.Threading.Tasks;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     删除标签
    /// </summary>
    public partial class DeleteTag : Controls.PopupWidget {
        private string repo = null;
        private string tag = null;

        public DeleteTag(string repo, string tag) {
            this.repo = repo;
            this.tag = tag;

            InitializeComponent();

            txtTag.Text = tag;
        }

        public override string GetTitle() {
            return App.Text("DeleteTag");
        }

        public override Task<bool> Start() {
            var push = chkPush.IsChecked == true;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.Tag(repo).Delete(tag, push);
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
