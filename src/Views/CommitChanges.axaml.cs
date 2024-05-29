using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class CommitChanges : UserControl
    {
        public CommitChanges()
        {
            InitializeComponent();
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail vm)
            {
                var selected = (sender as ChangeCollectionView)?.SelectedChanges;
                if (selected != null && selected.Count == 1)
                {
                    var menu = vm.CreateChangeContextMenu(selected[0]);
                    (sender as Control)?.OpenContextMenu(menu);
                }
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
    }
}
