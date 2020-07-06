using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SourceGit.UI {

    /// <summary>
    ///     Confirm to revert selected commit.
    /// </summary>
    public partial class Revert : UserControl {
        private Git.Repository repo = null;
        private string sha = null;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="opened">Opened repository</param>
        /// <param name="commit">Commit to be reverted</param>
        public Revert(Git.Repository opened, Git.Commit commit) {
            repo = opened;
            sha = commit.SHA;

            InitializeComponent();
            txtDesc.Content = $"{commit.ShortSHA}  {commit.Subject}";
        }

        /// <summary>
        ///     Open this dialog.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="commit"></param>
        public static void Show(Git.Repository repo, Git.Commit commit) {
            PopupManager.Show(new Revert(repo, commit));
        }

        /// <summary>
        ///     Start revert.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Sure(object sender, RoutedEventArgs e) {
            bool autoCommit = chkCommit.IsChecked == true;

            PopupManager.Lock();

            status.Visibility = Visibility.Visible;
            DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
            anim.RepeatBehavior = RepeatBehavior.Forever;
            statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);

            await Task.Run(() => repo.Revert(sha, autoCommit));

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
