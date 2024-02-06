using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views {
    public partial class CaptionButtonsMacOS : UserControl {
        public CaptionButtonsMacOS() {
            InitializeComponent();
        }

        private void MinimizeWindow(object sender, RoutedEventArgs e) {
            var window = this.FindAncestorOfType<Window>();
            if (window != null) {
                window.WindowState = WindowState.Minimized;
            }
        }

        private void MaximizeOrRestoreWindow(object sender, RoutedEventArgs e) {
            var window = this.FindAncestorOfType<Window>();
            if (window != null) {
                window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
        }

        private void CloseWindow(object sender, RoutedEventArgs e) {
            var window = this.FindAncestorOfType<Window>();
            if (window != null) {
                window.Close();
            }
        }
    }
}

