using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class FileHistories : Window
    {
        public FileHistories()
        {
            InitializeComponent();
        }

        private void MaximizeOrRestoreWindow(object sender, TappedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
            e.Handled = true;
        }

        private void CustomResizeWindow(object sender, PointerPressedEventArgs e)
        {
            if (sender is Border border)
            {
                if (border.Tag is WindowEdge edge)
                {
                    BeginResizeDrag(edge, e);
                }
            }
        }

        private void BeginMoveWindow(object sender, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }
    }
}
