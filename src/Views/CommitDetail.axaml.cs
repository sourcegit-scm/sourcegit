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

        private void OnChangeDoubleTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail && sender is Grid grid && grid.DataContext is Models.Change change)
            {
                detail.ActivePageIndex = 1;
                detail.SelectedChanges = new() { change };
            }

            e.Handled = true;
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail && sender is Grid grid && grid.DataContext is Models.Change change)
            {
                var menu = detail.CreateChangeContextMenu(change);
                menu?.Open(grid);
            }

            e.Handled = true;
        }
    }
}
