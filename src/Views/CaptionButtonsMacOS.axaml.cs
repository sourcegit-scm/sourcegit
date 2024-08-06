using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class CaptionButtonsMacOS : UserControl
    {
        public CaptionButtonsMacOS()
        {
            InitializeComponent();
        }

        private void MinimizeWindow(object _, RoutedEventArgs e)
        {
            var window = this.FindAncestorOfType<Window>();
            if (window != null)
                window.WindowState = WindowState.Minimized;

            e.Handled = true;
        }

        private void MaximizeOrRestoreWindow(object _, RoutedEventArgs e)
        {
            var window = this.FindAncestorOfType<Window>();
            if (window != null)
                window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

            e.Handled = true;
        }

        private void CloseWindow(object _, RoutedEventArgs e)
        {
            var window = this.FindAncestorOfType<Window>();
            if (window != null)
                window.Close();

            e.Handled = true;
        }
    }
}
