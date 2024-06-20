using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class CommitDetail : UserControl
    {
        public CommitDetail()
        {
            InitializeComponent();
        }

        private void OnChangeListDoubleTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail)
            {
                var datagrid = sender as DataGrid;
                if (datagrid.SelectedItem == null)
                {
                    e.Handled = true;
                    return;
                }

                detail.ActivePageIndex = 1;
                detail.SelectedChanges = new() { datagrid.SelectedItem as Models.Change };
            }

            e.Handled = true;
        }

        private void OnChangeListContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail)
            {
                var datagrid = sender as DataGrid;
                if (datagrid.SelectedItem == null)
                {
                    e.Handled = true;
                    return;
                }

                var menu = detail.CreateChangeContextMenu(datagrid.SelectedItem as Models.Change);
                datagrid.OpenContextMenu(menu);
            }

            e.Handled = true;
        }
    }
}
