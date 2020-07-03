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
        ///     Current app version
        /// </summary>
        public string Version {
            get {
                return FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
            }
        }

        /// <summary>
        ///     Constructor
        /// </summary>
        public About() {
            InitializeComponent();
        }

        /// <summary>
        ///     Open source code link
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenSource(object sender, RequestNavigateEventArgs e) {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
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
