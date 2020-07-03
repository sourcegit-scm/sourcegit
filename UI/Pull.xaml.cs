using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SourceGit.UI {

    /// <summary>
    ///     Git pull
    /// </summary>
    public partial class Pull : UserControl {
        private Git.Repository repo = null;
        private string preferRemote = null;
        private string preferBranch = null;

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="opened">Opened repository</param>
        /// <param name="preferRemoteBranch">Prefered remote branch.</param>
        public Pull(Git.Repository opened, string preferRemoteBranch) {
            repo = opened;
            InitializeComponent();
            SetContent(preferRemoteBranch);
        }

        /// <summary>
        ///     Display git pull dialog.
        /// </summary>
        /// <param name="opened">Opened repository</param>
        /// <param name="preferRemoteBranch">Prefered remote branch</param>
        public static void Show(Git.Repository opened, string preferRemoteBranch = null) {
            PopupManager.Show(new Pull(opened, preferRemoteBranch));
        }

        /// <summary>
        ///     Set content.
        /// </summary>
        private void SetContent(string prefered) {
            var branches = repo.Branches();
            var remotes = new List<string>();
            var current = null as Git.Branch;

            foreach (var b in branches) {
                if (b.IsLocal) {
                    if (b.IsCurrent) current = b;
                } else {
                    if (!remotes.Contains(b.Remote)) remotes.Add(b.Remote);
                }
            }

            if (!string.IsNullOrEmpty(prefered)) {
                preferRemote = prefered.Substring(0, prefered.IndexOf('/'));
                preferBranch = prefered;
            } else if (current != null && !string.IsNullOrEmpty(current.Upstream)) {
                var upstream = current.Upstream.Substring("refs/remotes/".Length);
                preferRemote = upstream.Substring(0, upstream.IndexOf('/'));
                preferBranch = upstream;
            }

            txtInto.Content = current.Name;
            combRemotes.ItemsSource = remotes;
            combRemotes.SelectedItem = preferRemote;
        }

        /// <summary>
        ///     Start pull
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Start(object sender, RoutedEventArgs e) {
            var remote = combRemotes.SelectedItem as string;
            var branch = combBranches.SelectedItem as string;
            var rebase = chkRebase.IsChecked == true;
            var autoStash = chkAutoStash.IsChecked == true;

            if (remote == null || branch == null) return;

            PopupManager.Lock();

            status.Visibility = Visibility.Visible;
            DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
            anim.RepeatBehavior = RepeatBehavior.Forever;
            statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);

            await Task.Run(() => repo.Pull(remote, branch.Substring(branch.IndexOf('/')+1), msg => Dispatcher.Invoke(() => statusMsg.Content = msg), rebase, autoStash));

            status.Visibility = Visibility.Collapsed;
            statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            PopupManager.Close(true);
        }

        /// <summary>
        ///     Cancel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel(object sender, RoutedEventArgs e) {
            PopupManager.Close();
        }

        /// <summary>
        ///     Remote selection changed event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemotesSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count != 1) return;

            var remote = e.AddedItems[0] as string;
            var allBranches = repo.Branches();
            var branches = new List<string>();

            foreach (var b in allBranches) {
                if (!b.IsLocal && b.Remote == remote) {
                    branches.Add(b.Name);
                }
            }

            combBranches.ItemsSource = branches;
            if (remote == preferRemote && preferBranch != null) {
                combBranches.SelectedItem = preferBranch;
            } else {
                combBranches.SelectedIndex = 0;
            }
        }
    }
}
