using Avalonia.Controls;

namespace SourceGit.Views
{
    public partial class StashesPage : UserControl
    {
        public StashesPage()
        {
            InitializeComponent();
        }

        private void OnStashContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.StashesPage vm && sender is Border border)
            {
                var menu = vm.MakeContextMenu(border.DataContext as Models.Stash);
                border.OpenContextMenu(menu);
            }
            e.Handled = true;
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e) 
        {
            if (DataContext is ViewModels.StashesPage vm && sender is Grid grid)
            {
                var menu = vm.MakeContextMenuForChange(grid.DataContext as Models.Change);
                grid.OpenContextMenu(menu);
            }
            e.Handled = true;
        }
    }
}
