using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SourceGit.Views.Popups {

    /// <summary>
    ///     拉回
    /// </summary>
    public partial class Pull : Controls.PopupWidget {
        private Models.Repository repo = null;
        private Models.Branch prefered = null;

        public Pull(Models.Repository repo, Models.Branch preferRemoteBranch) {
            this.repo = repo;
            this.prefered = preferRemoteBranch;

            InitializeComponent();

            var current = repo.Branches.Find(x => x.IsCurrent);
            if (current == null) return;

            txtInto.Text = current.Name;

            if (prefered == null && !string.IsNullOrEmpty(current.Upstream)) {
                prefered = repo.Branches.Find(x => x.FullName == current.Upstream);
            }

            cmbRemotes.ItemsSource = repo.Remotes;
            if (prefered != null) {
                cmbRemotes.SelectedItem = repo.Remotes.Find(x => x.Name == prefered.Remote);
            } else {
                cmbRemotes.SelectedItem = repo.Remotes[0];
            }
        }

        public override string GetTitle() {
            return App.Text("Pull.Title");
        }

        public override Task<bool> Start() {
            var branch = cmbBranches.SelectedItem as Models.Branch;
            if (branch == null) return null;

            var rebase = chkUseRebase.IsChecked == true;
            var autoStash = chkAutoStash.IsChecked == true;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo.Path, false);
                var succ = new Commands.Pull(repo.Path, branch.Remote, branch.Name, rebase, autoStash, UpdateProgress).Exec();
                Models.Watcher.SetEnabled(repo.Path, true);
                return succ;
            });
        }

        private void OnRemoteSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var remote = cmbRemotes.SelectedItem as Models.Remote;
            if (remote == null) return;

            var branches = repo.Branches.Where(x => x.Remote == remote.Name).ToList();
            cmbBranches.ItemsSource = branches;

            if (prefered != null && remote.Name == prefered.Remote) {
                cmbBranches.SelectedItem = branches.Find(x => x.FullName == prefered.FullName);
            } else {
                cmbBranches.SelectedItem = branches[0];
            }
        }
    }
}
