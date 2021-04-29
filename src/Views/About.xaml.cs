using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace SourceGit.Views {

    /// <summary>
    ///     关于对话框
    /// </summary>
    public partial class About : Window {

        public About() {
            InitializeComponent();

            var asm = Assembly.GetExecutingAssembly().GetName();
            var framework = AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName;
            var dotnetVer = framework.Substring(framework.IndexOf("=") + 1);

            version.Text = $"VERSION : v{asm.Version.Major}.{asm.Version.Minor}   .NET : {dotnetVer}";
        }

        private void OnRequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e) {
            var info = new ProcessStartInfo("cmd", $"/c start {e.Uri.AbsoluteUri}");
            info.CreateNoWindow = true;
            Process.Start(info);
        }

        private void Quit(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
