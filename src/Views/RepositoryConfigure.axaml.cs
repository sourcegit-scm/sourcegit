using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class RepositoryConfigure : ChromelessWindow
    {
        public RepositoryConfigure()
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

        private void SaveAndClose(object _1, RoutedEventArgs _2)
        {
            (DataContext as ViewModels.RepositoryConfigure)?.Save();
            Close();
        }
    }
}
