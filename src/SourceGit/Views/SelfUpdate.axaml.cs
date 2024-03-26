using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class SelfUpdate : Window
    {
        public SelfUpdate()
        {
            InitializeComponent();
        }

        private void BeginMoveWindow(object sender, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GotoDownload(object sender, RoutedEventArgs e)
        {
            Native.OS.OpenBrowser("https://github.com/sourcegit-scm/sourcegit/releases/latest");
            e.Handled = true;
        }

        private void IgnoreThisVersion(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var ver = button.DataContext as Models.Version;
            ViewModels.Preference.Instance.IgnoreUpdateTag = ver.TagName;
            Close();
            e.Handled = true;
        }
    }
}
