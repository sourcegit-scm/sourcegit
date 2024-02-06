using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views {
    public partial class CommitDetail : UserControl {
        public CommitDetail() {
            InitializeComponent();
        }

        private void OnChangeListDoubleTapped(object sender, TappedEventArgs e) {
            if (DataContext is ViewModels.CommitDetail detail) {
                var datagrid = sender as DataGrid;
                detail.ActivePageIndex = 1;
                detail.SelectedChange = datagrid.SelectedItem as Models.Change;
            }
            e.Handled = true;
        }

        private void OnChangeListContextRequested(object sender, ContextRequestedEventArgs e) {
            if (DataContext is ViewModels.CommitDetail detail) {
                var datagrid = sender as DataGrid;
                var menu = detail.CreateChangeContextMenu(datagrid.SelectedItem as Models.Change);
                menu.Open(datagrid);
            }
            e.Handled = true;
        }
    }
}
