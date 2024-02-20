using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Reflection;

namespace SourceGit.Views {
    public partial class About : Window {
        public string Version {
            get;
            private set;
        }

        public About() {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            Version = $"{ver.Major}.{ver.Minor}";
            DataContext = this;
            InitializeComponent();
        }

        private void CloseWindow(object sender, RoutedEventArgs e) {
            Close();
        }

        private void OnVisitAvaloniaUI(object sender, PointerPressedEventArgs e) {
            Native.OS.OpenBrowser("https://www.avaloniaui.net/");
            e.Handled = true;
        }

        private void OnVisitAvaloniaEdit(object sender, PointerPressedEventArgs e) {
            Native.OS.OpenBrowser("https://github.com/AvaloniaUI/AvaloniaEdit");
            e.Handled = true;
        }

        private void OnVisitJetBrainsMonoFont(object sender, PointerPressedEventArgs e) {
            Native.OS.OpenBrowser("https://www.jetbrains.com/lp/mono/");
            e.Handled = true;
        }
    }
}
