using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace SourceGit.Views.Popups {

    /// <summary>
    ///     创建分支面板
    /// </summary>
    public partial class CreateTag : Controls.PopupWidget {
        private string repo = null;
        private string basedOn = null;

        public string TagName { get; set; }
        public string Message { get; set; }

        public CreateTag(Models.Repository repo, Models.Branch branch) {
            this.repo = repo.Path;
            this.basedOn = branch.Head;

            InitializeComponent();

            ruleTag.Tags = new Commands.Tags(repo.Path).Result();
            iconBased.Data = FindResource("Icon.Branch") as Geometry;
            txtBased.Text = !string.IsNullOrEmpty(branch.Remote) ? $"{branch.Remote}/{branch.Name}" : branch.Name;
        }

        public CreateTag(Models.Repository repo, Models.Commit commit) {
            this.repo = repo.Path;
            this.basedOn = commit.SHA;

            InitializeComponent();

            ruleTag.Tags = new Commands.Tags(repo.Path).Result();
            iconBased.Data = FindResource("Icon.Commit") as Geometry;
            txtSHA.Text = commit.ShortSHA;
            txtBased.Text = commit.Subject;
            badgeSHA.Visibility = System.Windows.Visibility.Visible;
        }

        public override string GetTitle() {
            return App.Text("CreateTag");
        }

        public override Task<bool> Start() {
            txtTagName.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtTagName)) return null;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                new Commands.Tag(repo).Add(TagName, basedOn, Message);
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
