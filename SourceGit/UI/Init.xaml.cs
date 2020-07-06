using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SourceGit.UI {

    /// <summary>
    ///     `git init` confirm panel.
    /// </summary>
    public partial class Init : UserControl {
        private string workingDir = null;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="path"></param>
        public Init(string path) {
            workingDir = path;
            InitializeComponent();
            txtPath.Content = path;
        }

        /// <summary>
        ///     Show this dialog.
        /// </summary>
        /// <param name="path"></param>
        public static void Show(string path) {
            PopupManager.Show(new Init(path));
        }

        /// <summary>
        ///     Do `git init`
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Sure(object sender, RoutedEventArgs e) {
            PopupManager.Lock();

            DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
            anim.RepeatBehavior = RepeatBehavior.Forever;
            statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);
            status.Visibility = Visibility.Visible;

            await Task.Run(() => {
                var errs = Git.Repository.RunCommand(workingDir, "init -q", null);
                if (errs != null) {
                    App.RaiseError(errs);
                } else {
                    App.Preference.AddRepository(workingDir, "");
                }
            });

            status.Visibility = Visibility.Collapsed;
            statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            PopupManager.Close(true);

            var repo = App.Preference.FindRepository(workingDir);
            if (repo != null) repo.Open();
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
