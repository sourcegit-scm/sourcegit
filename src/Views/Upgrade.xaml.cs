using System.Diagnostics;
using System.Windows;

namespace SourceGit.Views {
    /// <summary>
    ///     新版本提示窗口
    /// </summary>
    public partial class Upgrade : Controls.Window {
        public Models.Version Version { get; set; } = new Models.Version();

        public Upgrade(Models.Version ver) {
            Version = ver;
            InitializeComponent();
            txtRelease.Text = App.Text("UpdateAvailable.Title", ver.Name);
        }

        private void Download(object sender, RoutedEventArgs e) {
            var url = $"https://github.com/sourcegit-scm/sourcegit/releases/{Version.TagName}";
#if NET48
            Process.Start(url);
#else
            var info = new ProcessStartInfo("cmd", $"/c start {url}");
            info.CreateNoWindow = true;
            Process.Start(info);
#endif
        }

        private void Quit(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
