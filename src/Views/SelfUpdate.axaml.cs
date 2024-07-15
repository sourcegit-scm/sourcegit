using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class SelfUpdate : ChromelessWindow
    {
        public SelfUpdate()
        {
            InitializeComponent();
        }

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Close();
        }

        private void GotoDownload(object _, RoutedEventArgs e)
        {
            Native.OS.OpenBrowser("https://github.com/sourcegit-scm/sourcegit/releases/latest");
            e.Handled = true;
        }

        private void IgnoreThisVersion(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: Models.Version ver })
                ViewModels.Preference.Instance.IgnoreUpdateTag = ver.TagName;

            Close();
            e.Handled = true;
        }
    }
}
