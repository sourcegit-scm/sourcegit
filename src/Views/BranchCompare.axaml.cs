using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class BranchCompare : ChromelessWindow
    {
        public BranchCompare()
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

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.BranchCompare vm && sender is ChangeCollectionView view)
            {
                var menu = vm.CreateChangeContextMenu();
                view.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OnPressedSHA(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is ViewModels.BranchCompare vm && sender is TextBlock block)
                vm.NavigateTo(block.Text);

            e.Handled = true;
        }

        private bool _pressedTitleBar = false;
    }
}
