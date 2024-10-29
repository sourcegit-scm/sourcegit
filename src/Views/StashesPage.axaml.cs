using Avalonia.Controls;

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

            var layout = ViewModels.Preference.Instance.Layout;
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
