using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SourceGit.UI {

    /// <summary>
    ///     Rebase current branch on selected commit/branch
    /// </summary>
    public partial class Rebase : UserControl {
        private Git.Repository repo = null;
        private string based = null;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="opened">Opened repository</param>
        public Rebase(Git.Repository opened) {
            repo = opened;
            InitializeComponent();
        }

        /// <summary>
        ///     Rebase current branch on selected branch
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="branch"></param>
        public static void Show(Git.Repository opened, Git.Branch branch) {
            if (branch == null) return;

            var current = opened.CurrentBranch();
            if (current == null) return;

            var dialog = new Rebase(opened);
            dialog.based = branch.Head;
            dialog.branch.Content = current.Name;
            dialog.type.Data = dialog.FindResource("Icon.Branch") as Geometry;
            dialog.desc.Content = branch.Name;
            PopupManager.Show(dialog);
        }

        /// <summary>
        ///     Rebase current branch on selected commit.
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="commit"></param>
        public static void Show(Git.Repository opened, Git.Commit commit) {
            var current = opened.CurrentBranch();
            if (current == null) return;

            var dialog = new Rebase(opened);
            dialog.based = commit.SHA;
            dialog.branch.Content = current.Name;
            dialog.type.Data = dialog.FindResource("Icon.Commit") as Geometry;
            dialog.desc.Content = $"{commit.ShortSHA}  {commit.Subject}";
            PopupManager.Show(dialog);
        }

        /// <summary>
        ///     Start rebase.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Start(object sender, RoutedEventArgs e) {
            PopupManager.Lock();

            DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
            anim.RepeatBehavior = RepeatBehavior.Forever;
            statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);
            status.Visibility = Visibility.Visible;

            var autoStash = chkAutoStash.IsChecked == true;
            await Task.Run(() => repo.Rebase(based, autoStash));

            status.Visibility = Visibility.Collapsed;
            statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            PopupManager.Close(true);
        }

        private void Cancel(object sender, RoutedEventArgs e) {
            PopupManager.Close();
        }
    }
}
