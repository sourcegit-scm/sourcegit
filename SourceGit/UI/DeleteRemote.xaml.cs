using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SourceGit.UI {

    /// <summary>
    ///     Confirm to delete a remote
    /// </summary>
    public partial class DeleteRemote : UserControl {
        private Git.Repository repo = null;
        private string remote = null;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="opened">Opened repository</param>
        /// <param name="target">Remote to be deleted</param>
        public DeleteRemote(Git.Repository opened, string target) {
            InitializeComponent();
            repo = opened;
            remote = target;
            remoteName.Content = target;
        }

        /// <summary>
        ///     Show this dialog
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="remote"></param>
        public static void Show(Git.Repository opened, string remote) {
            PopupManager.Show(new DeleteRemote(opened, remote));
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

            await Task.Run(() => Git.Remote.Delete(repo, remote));

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
