using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class WorkingCopy : UserControl
    {
        public WorkingCopy()
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

            if (layout.WorkingCopyLeftWidth.Value - maxLeft > 1.0)
                layout.WorkingCopyLeftWidth = new GridLength(maxLeft, GridUnitType.Pixel);
        }

        private async void OnOpenAssumeUnchanged(object sender, RoutedEventArgs e)
        {
            var repoView = this.FindAncestorOfType<Repository>();
            if (repoView is { DataContext: ViewModels.Repository repo })
                await App.ShowDialog(new ViewModels.AssumeUnchangedManager(repo));

            e.Handled = true;
        }

        private void OnUnstagedContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm && sender is Control control)
            {
                var container = control.FindDescendantOfType<ChangeCollectionContainer>();
                var selectedSingleFolder = string.Empty;
                if (container is { SelectedItems.Count: 1, SelectedItem: ViewModels.ChangeTreeNode { IsFolder: true } node })
                    selectedSingleFolder = node.FullPath;

                var menu = vm.CreateContextMenuForUnstagedChanges(selectedSingleFolder);
                menu?.Open(control);
                e.Handled = true;
            }
        }

        private void OnStagedContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm && sender is Control control)
            {
                var container = control.FindDescendantOfType<ChangeCollectionContainer>();
                var selectedSingleFolder = string.Empty;
                if (container is { SelectedItems.Count: 1, SelectedItem: ViewModels.ChangeTreeNode { IsFolder: true } node })
                    selectedSingleFolder = node.FullPath;

                var menu = vm.CreateContextMenuForStagedChanges(selectedSingleFolder);
                menu?.Open(control);
                e.Handled = true;
            }
        }

        private void OnUnstagedChangeDoubleTapped(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                vm.StageSelected(next);
                UnstagedChangesView.TakeFocus();
                e.Handled = true;
            }
        }

        private void OnStagedChangeDoubleTapped(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                var next = StagedChangesView.GetNextChangeWithoutSelection();
                vm.UnstageSelected(next);
                StagedChangesView.TakeFocus();
                e.Handled = true;
            }
        }

        private void OnUnstagedKeyDown(object _, KeyEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                if (e.Key is Key.Space or Key.Enter)
                {
                    var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                    vm.StageSelected(next);
                    UnstagedChangesView.TakeFocus();
                    e.Handled = true;
                    return;
                }

                if (e.Key is Key.Delete or Key.Back && vm.SelectedUnstaged is { Count: > 0 } selected)
                {
                    vm.Discard(selected);
                    e.Handled = true;
                }
            }
        }

        private void OnStagedKeyDown(object _, KeyEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm && e.Key is Key.Space or Key.Enter)
            {
                var next = StagedChangesView.GetNextChangeWithoutSelection();
                vm.UnstageSelected(next);
                StagedChangesView.TakeFocus();
                e.Handled = true;
            }
        }

        private void OnStageSelectedButtonClicked(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                vm.StageSelected(next);
                UnstagedChangesView.TakeFocus();
            }

            e.Handled = true;
        }

        private void OnUnstageSelectedButtonClicked(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                var next = StagedChangesView.GetNextChangeWithoutSelection();
                vm.UnstageSelected(next);
                StagedChangesView.TakeFocus();
            }

            e.Handled = true;
        }
    }
}
