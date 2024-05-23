using Avalonia.Controls;

namespace SourceGit.Views
{
    public partial class CommitChanges : UserControl
    {
        public CommitChanges()
        {
            InitializeComponent();
        }

        private void OnDataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid datagrid && datagrid.IsVisible && datagrid.SelectedItem != null)
            {
                datagrid.ScrollIntoView(datagrid.SelectedItem, null);
            }
            e.Handled = true;
        }

        private void OnDataGridContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is DataGrid datagrid && datagrid.SelectedItem != null)
            {
                var detail = DataContext as ViewModels.CommitDetail;
                var menu = detail.CreateChangeContextMenu(datagrid.SelectedItem as Models.Change);
                datagrid.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OnTreeViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is TreeView view && view.SelectedItem != null)
            {
                var detail = DataContext as ViewModels.CommitDetail;
                var node = view.SelectedItem as ViewModels.FileTreeNode;
                if (node != null && !node.IsFolder)
                {
                    var menu = detail.CreateChangeContextMenu(node.Backend as Models.Change);
                    view.OpenContextMenu(menu);
                }
            }

            e.Handled = true;
        }
    }
}
