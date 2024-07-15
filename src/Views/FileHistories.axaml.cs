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

        private void MaximizeOrRestoreWindow(object _, TappedEventArgs e)
        {
            _pressedTitleBar = false;

            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;

            e.Handled = true;
        }

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            if (e.ClickCount != 2)
                _pressedTitleBar = true;
        }

        private void MoveWindow(object _, PointerEventArgs e)
        {
            if (!_pressedTitleBar || e.Source == null)
                return;

            var visual = (Visual)e.Source;
            if (visual == null)
                return;

#pragma warning disable CS0618
            BeginMoveDrag(new PointerPressedEventArgs(
                e.Source,
                e.Pointer,
                visual,
                e.GetPosition(visual),
                e.Timestamp,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed),
                e.KeyModifiers));
#pragma warning restore CS0618
        }

        private void EndMoveWindow(object _1, PointerReleasedEventArgs _2)
        {
            _pressedTitleBar = false;
        }

        private bool _pressedTitleBar = false;
    }
}
