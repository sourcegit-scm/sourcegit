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

        private void OnUnstagedChangeDoubleTapped(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                vm.StageSelected(next);
                UnstagedChangesView.Focus();
                e.Handled = true;
            }
        }

        private void OnStagedChangeDoubleTapped(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                var next = StagedChangesView.GetNextChangeWithoutSelection();
                vm.UnstageSelected(next);
                StagedChangesView.Focus();
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
                    UnstagedChangesView.Focus();
                    e.Handled = true;
                    return;
                }

                if (e.Key is Key.Delete or Key.Back && vm.SelectedUnstaged is { Count: > 0 } selected)
                {
                    vm.Discard(selected);
                    e.Handled = true;
                    return;
                }
            }
        }

        private void OnStagedKeyDown(object _, KeyEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm && e.Key is Key.Space or Key.Enter)
            {
                var next = StagedChangesView.GetNextChangeWithoutSelection();
                vm.UnstageSelected(next);
                StagedChangesView.Focus();
                e.Handled = true;
            }
        }

        private void OnStageSelectedButtonClicked(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                vm.StageSelected(next);
                UnstagedChangesView.Focus();
            }

            e.Handled = true;
        }

        private void OnUnstageSelectedButtonClicked(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                var next = StagedChangesView.GetNextChangeWithoutSelection();
                vm.UnstageSelected(next);
                StagedChangesView.Focus();
            }

            e.Handled = true;
        }

        private void OnOpenAIAssist(object _, RoutedEventArgs e)
        {
            if (!Models.OpenAI.IsValid)
            {
                App.RaiseException(null, "Bad configuration for OpenAI");
                return;
            }

            if (DataContext is ViewModels.WorkingCopy vm)
            {
                if (vm.Staged is { Count: > 0 })
                {
                    var dialog = new AIAssistant() { DataContext = vm };
                    dialog.GenerateCommitMessage();
                    App.OpenDialog(dialog);
                }
                else
                {
                    App.RaiseException(null, "No files added to commit!");
                }
            }
            
            e.Handled = true;
        }
    }
}
