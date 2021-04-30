using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     新建分支面板
    /// </summary>
    public partial class CreateBranch : Controls.PopupWidget {
        private string repo = null;
        private string basedOn = null;

        public string BranchName { get; set; }
        public bool AutoStash { get; set; }

        public CreateBranch(Models.Repository repo, Models.Branch branch) {
            this.repo = repo.Path;
            this.basedOn = branch.Head;

            InitializeComponent();

            ruleBranch.Repo = repo;
            iconBased.Data = FindResource("Icon.Branch") as Geometry;
            txtBased.Text = !string.IsNullOrEmpty(branch.Remote) ? $"{branch.Remote}/{branch.Name}" : branch.Name;
        }

        public CreateBranch(Models.Repository repo, Models.Commit commit) {
            this.repo = repo.Path;
            this.basedOn = commit.SHA;

            InitializeComponent();

            ruleBranch.Repo = repo;
            iconBased.Data = FindResource("Icon.Commit") as Geometry;
            txtBased.Text = $"{commit.ShortSHA}  {commit.Subject}";
        }

        public CreateBranch(Models.Repository repo, Models.Tag tag) {
            this.repo = repo.Path;
            this.basedOn = tag.SHA;

            InitializeComponent();

            ruleBranch.Repo = repo;
            iconBased.Data = FindResource("Icon.Tag") as Geometry;
            txtBased.Text = tag.Name;
        }

        public override string GetTitle() {
            return App.Text("CreateBranch");
        }

        public override Task<bool> Start() {
            txtBranchName.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtBranchName)) return null;

            var checkout = chkCheckout.IsChecked == true;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                if (checkout) {
                    if (AutoStash) {
                        var changes = new Commands.LocalChanges(repo).Result();
                        if (changes.Count > 0) {
                            if (!new Commands.Stash(repo).Push(changes, "NEWBRANCH_AUTO_STASH")) {
                                return false;
                            }
                        } else {
                            AutoStash = true;
                        }
                    }

                    new Commands.Checkout(repo).Branch(BranchName, basedOn);
                    if (AutoStash) new Commands.Stash(repo).Pop("stash@{0}");
                } else {
                    new Commands.Branch(repo, BranchName).Create(basedOn);
                }
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
