using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     新建分支面板
    /// </summary>
    public partial class CreateBranch : Controls.PopupWidget {
        private string repo = null;
        private string basedOn = null;

        public string BranchName { get; set; } = "";
        public bool AutoStash { get; set; } = true;

        public CreateBranch(Models.Repository repo, Models.Branch branch) {
            this.repo = repo.Path;
            this.basedOn = branch.FullName;

            if (!branch.IsLocal) BranchName = branch.Name;

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
            txtSHA.Text = commit.ShortSHA;
            txtBased.Text = commit.Subject;
            badgeSHA.Visibility = Visibility.Visible;
        }

        public CreateBranch(Models.Repository repo, Models.Tag tag) {
            this.repo = repo.Path;
            this.basedOn = tag.Name;

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
                    var changes = new Commands.LocalChanges(repo).Result();
                    if (changes.Count > 0) {
                        if (AutoStash) {
                            if (!new Commands.Stash(repo).Push(changes, "NEWBRANCH_AUTO_STASH")) {
                                return false;
                            }
                        } else {
                            new Commands.Discard(repo).Whole();
                        }
                    } else {
                        AutoStash = false;
                    }

                    UpdateProgress($"Create new branch '{BranchName}'");
                    new Commands.Checkout(repo).Branch(BranchName, basedOn, UpdateProgress);
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
