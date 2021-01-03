using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace SourceGit.UI {

    /// <summary>
    /// Interaction logic for UpdateAvailable.xaml
    /// </summary>
    public partial class UpdateAvailable : Window {
        private string tag = null;

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="version"></param>
        public UpdateAvailable(Git.Version version) {
            InitializeComponent();

            txtRelease.Content = $"{version.Name} is available!";
            txtTime.Content = version.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            txtBasedOn.Content = version.CommitSHA.Substring(0, 10);
            txtPrerelease.Content = version.PreRelease ? "YES" : "NO";
            txtChangeLog.Text = version.Body;
            tag = version.TagName;
        }

        /// <summary>
        ///     Open source code link
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Download(object sender, RoutedEventArgs e) {
            Process.Start($"https://gitee.com/sourcegit/SourceGit/releases/{tag}");
            //Process.Start(new ProcessStartInfo("cmd", $"/c start https://gitee.com/sourcegit/SourceGit/releases/{tag}") { CreateNoWindow = true });
            e.Handled = true;
        }

        /// <summary>
        ///     Close this dialog
        /// </summary>
        private void Quit(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
