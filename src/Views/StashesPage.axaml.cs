using System;
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

        private async void OnStashListKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is ViewModels.StashesPage { SelectedStash: { } stash } vm)
            {
                if (e.Key is Key.Delete or Key.Back)
                {
                    vm.Drop(stash);
                    e.Handled = true;
                }
                else if (e.Key is Key.C && e.KeyModifiers == (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
                {
                    await App.CopyTextAsync(stash.Message);
                    e.Handled = true;
                }
            }
        }

        private void OnStashContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.StashesPage vm &&
                sender is Border { DataContext: Models.Stash stash } border)
            {
                var menu = vm.MakeContextMenu(stash);
                menu.Open(border);
            }

            e.Handled = true;
        }

        private void OnStashDoubleTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.StashesPage vm &&
                sender is Border { DataContext: Models.Stash stash })
                vm.Apply(stash);

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
