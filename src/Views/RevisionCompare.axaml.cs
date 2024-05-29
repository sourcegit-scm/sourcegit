using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class RevisionCompare : UserControl
    {
        public RevisionCompare()
        {
            InitializeComponent();
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.RevisionCompare vm && sender is ChangeCollectionView view)
            {
                var menu = vm.CreateChangeContextMenu();
                view.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OnChangeDoubleTapped(object sender, RoutedEventArgs e)
        {
            if (sender is ChangeCollectionView view)
            {
                var selected = view.tree?.RowSelection?.SelectedItem as ViewModels.FileTreeNode;
                if (selected != null && selected.IsFolder)
                    selected.IsExpanded = !selected.IsExpanded;
            }

            e.Handled = true;
        }

        private void OnPressedSHA(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is ViewModels.RevisionCompare vm && sender is TextBlock block)
                vm.NavigateTo(block.Text);

            e.Handled = true;
        }
    }
}
