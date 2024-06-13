using Avalonia;
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
            _pressedTitleBar = false;

            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;

            e.Handled = true;
        }

        private void BeginMoveWindow(object sender, PointerPressedEventArgs e)
        {
            if (e.ClickCount != 2)
                _pressedTitleBar = true;
        }

        private void MoveWindow(object sender, PointerEventArgs e)
        {
            if (!_pressedTitleBar)
                return;

            var visual = (Visual)e.Source;
            BeginMoveDrag(new PointerPressedEventArgs(
                e.Source,
                e.Pointer,
                visual,
                e.GetPosition(visual),
                e.Timestamp,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed),
                e.KeyModifiers));
        }

        private void EndMoveWindow(object sender, PointerReleasedEventArgs e)
        {
            _pressedTitleBar = false;
        }

        private bool _pressedTitleBar = false;
    }
}
