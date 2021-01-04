using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace SourceGit.UI {

    /// <summary>
    ///     About dialog
    /// </summary>
    public partial class About : Window {

        /// <summary>
        ///     Constructor
        /// </summary>
        public About() {
            InitializeComponent();

            var asm = Assembly.GetExecutingAssembly().GetName();
            var framework = AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName;
            var dotnetVer = framework.Substring(framework.IndexOf("=") + 1);

            version.Content = $"VERSION : v{asm.Version.Major}.{asm.Version.Minor}   .NET : {dotnetVer}";
        }

        /// <summary>
        ///     Open source code link
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenSource(object sender, RequestNavigateEventArgs e) {
            Process.Start(e.Uri.AbsoluteUri);
            //Process.Start(new ProcessStartInfo("cmd", $"/c start {e.Uri.AbsoluteUri}") { CreateNoWindow = true });
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
