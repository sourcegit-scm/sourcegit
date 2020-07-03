using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SourceGit.UI {

    /// <summary>
    ///     Confirm to discard changes dialog.
    /// </summary>
    public partial class Discard : UserControl {
        private Git.Repository repo = null;
        private List<Git.Change> changes = null;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="targets"></param>
        public Discard(Git.Repository opened, List<Git.Change> targets) {
            repo = opened;
            changes = targets;

            InitializeComponent();

            if (changes == null || changes.Count == 0) {
                txtPath.Content = "All local changes in working copy.";
                icon.Data = FindResource("Icon.Folder") as Geometry;
            } else if (changes.Count == 1) {
                txtPath.Content = changes[0].Path;
            } else {
                txtPath.Content = $"Total {changes.Count} changes ...";
            }
        }

        /// <summary>
        ///     Show this dialog
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="targets"></param>
        public static void Show(Git.Repository opened, List<Git.Change> targets) {
            PopupManager.Show(new Discard(opened, targets));
        }

        private async void Sure(object sender, RoutedEventArgs e) {
            PopupManager.Lock();

            DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
            anim.RepeatBehavior = RepeatBehavior.Forever;
            statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);
            status.Visibility = Visibility.Visible;

            await Task.Run(() => repo.Discard(changes));

            status.Visibility = Visibility.Collapsed;
            statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            PopupManager.Close(true);
        }

        private void Cancel(object sender, RoutedEventArgs e) {
            PopupManager.Close();
        }
    }
}
