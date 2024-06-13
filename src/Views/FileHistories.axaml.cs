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

        private void MaximizeOrRestoreWindow(object sender, TappedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;

            e.Handled = true;
        }

        private void BeginMoveWindow(object sender, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }
    }
}
