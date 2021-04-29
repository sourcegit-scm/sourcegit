using System.Diagnostics;
using System.Windows;

namespace SourceGit.Views {
    /// <summary>
    ///     新版本提示窗口
    /// </summary>
    public partial class Upgrade : Window {
        public Models.Version Version { get; set; } = new Models.Version();

        public Upgrade(Models.Version ver) {
            Version = ver;
            InitializeComponent();
            txtRelease.Text = App.Text("UpdateAvailable.Title", ver.Name);
        }

        public static void Open(Window owner, Models.Version ver) {
            var dialog = new Upgrade(ver) { Owner = owner };
            dialog.ShowDialog();
        }

        private void Download(object sender, RoutedEventArgs e) {
            var info = new ProcessStartInfo("cmd", $"/c start https://gitee.com/sourcegit/SourceGit/releases/{Version.TagName}");
            info.CreateNoWindow = true;

            Process.Start(info);
            e.Handled = true;
        }

        private void Quit(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
