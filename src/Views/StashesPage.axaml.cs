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
            if (sender is not Grid grid)
                return;

            var layout = ViewModels.Preferences.Instance.Layout;
            var width = grid.Bounds.Width;
            var maxLeft = width - 304;

            if (layout.StashesLeftWidth.Value - maxLeft > 1.0)
                layout.StashesLeftWidth = new GridLength(maxLeft, GridUnitType.Pixel);
        }

        private void OnStashListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is not (Key.Delete or Key.Back))
                return;

            if (DataContext is not ViewModels.StashesPage vm)
                return;

            vm.Drop(vm.SelectedStash);
            e.Handled = true;
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

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.StashesPage vm && sender is ChangeCollectionView view)
            {
                var menu = vm.MakeContextMenuForChange();
                menu?.Open(view);
            }
            e.Handled = true;
        }
    }
}
