using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     推送
    /// </summary>
    public partial class Push : Controls.PopupWidget {
        private Models.Repository repo = null;

        public Push(Models.Repository repo, Models.Branch localBranch) {
            this.repo = repo;

            InitializeComponent();

            var localBranches = repo.Branches.Where(x => x.IsLocal).ToList();
            cmbLocalBranches.ItemsSource = localBranches;
            if (localBranch != null) cmbLocalBranches.SelectedItem = localBranch;
            else cmbLocalBranches.SelectedItem = localBranches.Find(x => x.IsCurrent);
        }

        public override string GetTitle() {
            return App.Text("Push.Title");
        }

        public override Task<bool> Start() {
            var localBranch = cmbLocalBranches.SelectedItem as Models.Branch;
            if (localBranch == null) return null;

            var remoteBranch = cmbRemoteBranches.SelectedItem as Models.Branch;
            if (remoteBranch == null) return null;

            var withTags = chkAllTags.IsChecked == true;
            var force = chkForce.IsChecked == true;
            var track = string.IsNullOrEmpty(localBranch.Upstream);

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo.Path, false);
                var succ = new Commands.Push(
                    repo.Path, 
                    localBranch.Name, 
                    remoteBranch.Remote, 
                    remoteBranch.Name.Replace(" (new)", ""), 
                    withTags, 
                    force,
                    track,
                    UpdateProgress).Exec();
                Models.Watcher.SetEnabled(repo.Path, true);
                return succ;
            });
        }

        private void OnLocalSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var local = cmbLocalBranches.SelectedItem as Models.Branch;
            if (local == null) return;

            cmbRemotes.ItemsSource = null;
            cmbRemotes.ItemsSource = repo.Remotes;

            if (!string.IsNullOrEmpty(local.Upstream)) {
                cmbRemotes.SelectedItem = repo.Remotes.Find(x => local.Upstream.StartsWith($"refs/remotes/{x.Name}/"));
            } else {
                cmbRemotes.SelectedItem = repo.Remotes[0];
            }
        }

        private void OnRemoteSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var local = cmbLocalBranches.SelectedItem as Models.Branch;
            if (local == null) return;

            var remote = cmbRemotes.SelectedItem as Models.Remote;
            if (remote == null) return;

            var remoteBranches = new List<Models.Branch>();
            remoteBranches.AddRange(repo.Branches.Where(x => x.Remote == remote.Name));
            cmbRemoteBranches.ItemsSource = null;

            if (!string.IsNullOrEmpty(local.Upstream)) {
                foreach (var b in remoteBranches) {
                    if (b.FullName == local.Upstream) {
                        cmbRemoteBranches.ItemsSource = remoteBranches;
                        cmbRemoteBranches.SelectedItem = b;
                        return;
                    }
                }
            }

            var match = $"refs/remotes/{remote.Name}/{local.Name}";
            foreach (var b in remoteBranches) {
                if (b.FullName == match) {
                    cmbRemoteBranches.ItemsSource = remoteBranches;
                    cmbRemoteBranches.SelectedItem = b;
                    return;
                }
            }

            var prefer = new Models.Branch() {
                Remote = remote.Name,
                Name = $"{local.Name} (new)"
            };
            remoteBranches.Add(prefer);
            cmbRemoteBranches.ItemsSource = remoteBranches;
            cmbRemoteBranches.SelectedItem = prefer;
        }
    }
}
