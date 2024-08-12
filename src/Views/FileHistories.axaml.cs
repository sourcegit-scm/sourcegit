using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class FileHistories : ChromelessWindow
    {
        public FileHistories()
        {
            InitializeComponent();
        }

        private void MaximizeOrRestoreWindow(object _, TappedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;

            e.Handled = true;
        }

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            if (e.ClickCount == 1)
                BeginMoveDrag(e);

            e.Handled = true;
        }
    }
}
