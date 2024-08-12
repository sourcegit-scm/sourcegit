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
    }
}
