using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SourceGit.UI {
    /// <summary>
    ///     Confirm to delete branch
    /// </summary>
    public partial class DeleteBranch : UserControl {
        private Git.Repository repo = null;
        private Git.Branch branch = null;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="opened">Opened repository.</param>
        /// <param name="target">Branch to be deleted.</param>
        public DeleteBranch(Git.Repository opened, Git.Branch target) {
            InitializeComponent();
            repo = opened;
            branch = target;
            branchName.Content = target.Name;
        }

        /// <summary>
        ///     Show this dialog.
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="branch"></param>
        public static void Show(Git.Repository opened, Git.Branch branch) {
            PopupManager.Show(new DeleteBranch(opened, branch));
        }

        /// <summary>
        ///     Delete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Sure(object sender, RoutedEventArgs e) {
            PopupManager.Lock();

            DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
            anim.RepeatBehavior = RepeatBehavior.Forever;
            statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);
            status.Visibility = Visibility.Visible;

            await Task.Run(() => branch.Delete(repo));

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
    }
}
