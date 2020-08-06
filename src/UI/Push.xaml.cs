using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Git push dialog
    /// </summary>
    public partial class Push : UserControl {
        private Git.Repository repo = null;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="opened">Opened repository.</param>
        /// <param name="prefer">Prefered push branch.</param>
        public Push(Git.Repository opened, Git.Branch prefer) {
            repo = opened;
            InitializeComponent();
            SetContent(prefer);
        }

        /// <summary>
        ///     Show push dialog.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="prefer"></param>
        public static void Show(Git.Repository repo, Git.Branch prefer = null) {
            repo.GetPopupManager()?.Show(new Push(repo, prefer));
        }

        /// <summary>
        ///     Show push and start directly.
        /// </summary>
        /// <param name="repo"></param>
        public static void StartDirectly(Git.Repository repo) {
            var current = repo.CurrentBranch();
            if (current == null || string.IsNullOrEmpty(current.Upstream)) {
                App.RaiseError("Current branch has no tracked upstream");
                return;
            }

            var push = new Push(repo, current);
            var popup = repo.GetPopupManager();
            popup?.Show(push);
            popup?.Lock();

            var upstream = current.Upstream.Substring(13);
            var remoteIdx = upstream.IndexOf('/');
            var remote = upstream.Substring(0, remoteIdx);
            var remoteBranch = upstream.Substring(remoteIdx + 1);

            Task.Run(() => {
                repo.Push(remote, current.Name, remoteBranch, msg => popup?.UpdateStatus(msg));
                push.Dispatcher.Invoke(() => {
                    popup?.Close(true);
                });                
            });            
        }

        /// <summary>
        ///     Set content.
        /// </summary>
        private void SetContent(Git.Branch prefer) {
            var allBranches = repo.Branches();
            var localBranches = new List<Git.Branch>();

            foreach (var b in allBranches) {
                if (b.IsLocal) {
                    localBranches.Add(b);
                    if (b.IsCurrent && prefer == null) prefer = b;
                }
            }

            combLocalBranches.ItemsSource = localBranches;
            combLocalBranches.SelectedItem = prefer;
        }

        /// <summary>
        ///     Start push.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Start(object sender, RoutedEventArgs e) {
            var localBranch = combLocalBranches.SelectedItem as Git.Branch;
            var remote = combRemotes.SelectedItem as string;
            var remoteBranch = combRemoteBranches.SelectedItem as string;
            var track = string.IsNullOrEmpty(localBranch.Upstream);
            var tags = chkTags.IsChecked == true;
            var force = chkForce.IsChecked == true;

            remoteBranch = remoteBranch.Substring($"{remote}/".Length);
            if (remoteBranch.Contains(" (new)")) {
                remoteBranch = remoteBranch.Substring(0, remoteBranch.Length - 6);
            }

            var popup = repo.GetPopupManager();
            popup?.Lock();
            await Task.Run(() => repo.Push(remote, localBranch.Name, remoteBranch, msg => popup?.UpdateStatus(msg), tags, track, force));
            popup?.Close(true);
        }

        /// <summary>
        ///     Cancel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel(object sender, RoutedEventArgs e) {
            repo.GetPopupManager()?.Close();
        }

        /// <summary>
        ///     Local branch selection changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LocalBranchesSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count != 1) return;

            var current = e.AddedItems[0] as Git.Branch;
            var allRemotes = repo.Remotes();
            var remoteNames = new List<string>();
            foreach (var r in allRemotes) remoteNames.Add(r.Name);
            combRemotes.ItemsSource = null;
            combRemotes.ItemsSource = remoteNames;

            if (!string.IsNullOrEmpty(current.Upstream)) {
                var upstream = current.Upstream.Substring("refs/remotes/".Length);
                combRemotes.SelectedItem = upstream.Substring(0, upstream.IndexOf('/'));
            } else {
                combRemotes.SelectedIndex = 0;
            }          
        }

        /// <summary>
        ///     Remote selection changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemotesSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count != 1) return;

            var remote = e.AddedItems[0] as string;
            var allBranches = repo.Branches();
            var branches = new List<string>();

            combRemoteBranches.ItemsSource = null;

            foreach (var b in allBranches) {
                if (!b.IsLocal && b.Remote == remote) {
                    branches.Add(b.Name);
                }
            }

            var current = combLocalBranches.SelectedItem as Git.Branch;
            if (string.IsNullOrEmpty(current.Upstream)) {
                var newBranch = $"{remote}/{current.Name} (new)";
                branches.Add(newBranch);
                combRemoteBranches.ItemsSource = branches;
                combRemoteBranches.SelectedItem = newBranch;
            } else if (current.Upstream.StartsWith($"refs/remotes/{remote}", StringComparison.Ordinal)) {
                combRemoteBranches.ItemsSource = branches;
                combRemoteBranches.SelectedItem = current.Upstream.Substring("refs/remotes/".Length);
            } else {
                var match = $"{remote}/{current.Name}";
                foreach (var b in branches) {
                    if (b == match) {
                        combRemoteBranches.ItemsSource = branches;
                        combRemoteBranches.SelectedItem = b;
                        return;
                    }
                }

                var newBranch = $"{remote}/{current.Name} (new)";
                branches.Add(newBranch);
                combRemoteBranches.ItemsSource = branches;
                combRemoteBranches.SelectedItem = newBranch;
            }
        }
    }
}
