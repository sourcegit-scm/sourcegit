using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class WorkingCopy : UserControl
    {
        public WorkingCopy()
        {
            InitializeComponent();
        }

        private void OnOpenCommitMessagePicker(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.WorkingCopy vm)
            {
                var menu = vm.CreateContextMenuForCommitMessages();
                menu.Placement = PlacementMode.TopEdgeAlignedLeft;
                button.OpenContextMenu(menu);
                e.Handled = true;
            }
        }

        private void OnUnstagedContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                var menu = vm.CreateContextMenuForUnstagedChanges();
                (sender as Control)?.OpenContextMenu(menu);
                e.Handled = true;
            }
        }

        private void OnStagedContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                var menu = vm.CreateContextMenuForStagedChanges();
                (sender as Control)?.OpenContextMenu(menu);
                e.Handled = true;
            }
        }

        private void OnUnstagedChangeDoubleTapped(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                vm.StageSelected();
                e.Handled = true;
            }
        }

        private void OnStagedChangeDoubleTapped(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                vm.UnstageSelected();
                e.Handled = true;
            }
        }

        private void OnUnstagedKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm && e.Key == Key.Space)
            {
                vm.StageSelected();
                e.Handled = true;
            }
        }

        private void OnStagedKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm && e.Key == Key.Space)
            {
                vm.UnstageSelected();
                e.Handled = true;
            }
        }
    }
}
