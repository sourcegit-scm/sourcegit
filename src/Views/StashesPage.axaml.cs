using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class StashesPage : UserControl
    {
        public StashesPage()
        {
            InitializeComponent();
        }

        private void OnMainLayoutSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var grid = sender as Grid;
            if (grid == null)
                return;

            var layout = ViewModels.Preferences.Instance.Layout;
            var width = grid.Bounds.Width;
            var maxLeft = width - 304;

            if (layout.StashesLeftWidth.Value - maxLeft > 1.0)
                layout.StashesLeftWidth = new GridLength(maxLeft, GridUnitType.Pixel);
        }

        private void OnStashContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.StashesPage vm && sender is Border border)
            {
                var menu = vm.MakeContextMenu(border.DataContext as Models.Stash);
                menu?.Open(border);
            }
            e.Handled = true;
        }

        private void OnStashKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is not (Key.Delete or Key.Back))
                return;

            if (DataContext is not ViewModels.StashesPage vm)
                return;

            if (sender is not ListBox { SelectedValue: Models.Stash stash })
                return;

            vm.Drop(stash);
            e.Handled = true;
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.StashesPage vm && sender is Grid grid)
            {
                var menu = vm.MakeContextMenuForChange(grid.DataContext as Models.Change);
                menu?.Open(grid);
            }
            e.Handled = true;
        }
    }
}
