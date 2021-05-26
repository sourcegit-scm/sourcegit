using System.Threading.Tasks;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     合并操作界面
    /// </summary>
    public partial class Merge : Controls.PopupWidget {
        private string repo = null;
        private string source = null;

        public Merge(string repo, string source, string dest) {
            this.repo = repo;
            this.source = source;

            InitializeComponent();

            txtSource.Text = source;
            txtInto.Text = dest;
        }

        public override string GetTitle() {
            return App.Text("Merge");
        }

        public override Task<bool> Start() {
            var mode = (cmbMode.SelectedItem as Models.MergeOption).Arg;
            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.Merge(repo, source, mode, UpdateProgress).Exec();
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
