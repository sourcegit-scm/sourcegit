using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;

namespace SourceGit.UI {

    /// <summary>
    ///     File histories panel.
    /// </summary>
    public partial class FileHistories : Window {
        private Git.Repository repo = null;
        private string file = null;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="file"></param>
        public FileHistories(Git.Repository repo, string file) {
            this.repo = repo;
            this.file = file;

            InitializeComponent();

            // Move to center
            var parent = App.Current.MainWindow;
            Left = parent.Left + (parent.Width - Width) * 0.5;
            Top = parent.Top + (parent.Height - Height) * 0.5;

            // Show loading
            DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
            anim.RepeatBehavior = RepeatBehavior.Forever;
            loading.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);
            loading.Visibility = Visibility.Visible;

            // Load commits
            Task.Run(() => {
                var commits = repo.Commits($"-n 10000 -- \"{file}\"");
                Dispatcher.Invoke(() => {
                    commitList.ItemsSource = commits;
                    commitList.SelectedIndex = 0;

                    loading.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
                    loading.Visibility = Visibility.Collapsed;
                });
            });
        }

        /// <summary>
        ///     Logo click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogoMouseButtonDown(object sender, MouseButtonEventArgs e) {
            var element = e.OriginalSource as FrameworkElement;
            if (element == null) return;

            var pos = PointToScreen(new Point(0, 33));
            SystemCommands.ShowSystemMenu(this, pos);
        }

        /// <summary>
        ///     Minimize
        /// </summary>
        private void Minimize(object sender, RoutedEventArgs e) {
            SystemCommands.MinimizeWindow(this);
        }

        /// <summary>
        ///     Maximize/Restore
        /// </summary>
        private void MaximizeOrRestore(object sender, RoutedEventArgs e) {
            if (WindowState == WindowState.Normal) {
                SystemCommands.MaximizeWindow(this);
            } else {
                SystemCommands.RestoreWindow(this);
            }
        }

        /// <summary>
        ///     Quit
        /// </summary>
        private void Quit(object sender, RoutedEventArgs e) {
            Close();
        }

        /// <summary>
        ///     Commit selection change event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommitSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count != 1) return;

            var commit = e.AddedItems[0] as Git.Commit;
            var start = $"{commit.SHA}^";
            if (commit.Parents.Count == 0) start = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

            diff.Diff(repo, new DiffViewer.Option() {
                RevisionRange = new string[] { start, commit.SHA },
                Path = file
            });
        }

        /// <summary>
        ///     Navigate to given string
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NavigateToCommit(object sender, RequestNavigateEventArgs e) {
            repo.OnNavigateCommit?.Invoke(e.Uri.OriginalString);
            e.Handled = true;
        }
    }
}
