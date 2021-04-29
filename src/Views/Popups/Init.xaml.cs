using System.Threading.Tasks;

namespace SourceGit.Views.Popups {

    /// <summary>
    ///     初始化Git仓库确认框
    /// </summary>
    public partial class Init : Controls.PopupWidget {
        public string WorkDir { get; set; }

        public Init(string dir) {
            WorkDir = dir;
            InitializeComponent();
        }

        public override string GetTitle() {
            return App.Text("Init");
        }

        public override Task<bool> Start() {
            return Task.Run(() => {
                var succ = new Commands.Init(WorkDir).Exec();
                if (!succ) return false;

                var repo = Models.Preference.Instance.AddRepository(WorkDir, WorkDir + "\\.git", "");
                Models.Watcher.Open(repo);
                return true;
            });
        }
    }
}
