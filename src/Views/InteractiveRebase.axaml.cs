using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class InteractiveRebase : ChromelessWindow
    {
        public InteractiveRebase()
        {
            InitializeComponent();
        }

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Close();
        }

        private void OnMoveItemUp(object sender, RoutedEventArgs e)
        {
            if (sender is Control control && DataContext is ViewModels.InteractiveRebase vm)
            {
                vm.MoveItemUp(control.DataContext as ViewModels.InteractiveRebaseItem);
                e.Handled = true;
            }
        }

        private void OnMoveItemDown(object sender, RoutedEventArgs e)
        {
            if (sender is Control control && DataContext is ViewModels.InteractiveRebase vm)
            {
                vm.MoveItemDown(control.DataContext as ViewModels.InteractiveRebaseItem);
                e.Handled = true;
            }
        }

        private void OnDataGridKeyDown(object sender, KeyEventArgs e)
        {
            var item = (sender as DataGrid)?.SelectedItem as ViewModels.InteractiveRebaseItem;
            if (item == null)
                return;

            if (e.Key == Key.P)
                item.SetAction(Models.InteractiveRebaseAction.Pick);
            else if (e.Key == Key.E)
                item.SetAction(Models.InteractiveRebaseAction.Edit);
            else if (e.Key == Key.R)
                item.SetAction(Models.InteractiveRebaseAction.Reword);
            else if (e.Key == Key.S)
                item.SetAction(Models.InteractiveRebaseAction.Squash);
            else if (e.Key == Key.F)
                item.SetAction(Models.InteractiveRebaseAction.Fixup);
            else if (e.Key == Key.D)
                item.SetAction(Models.InteractiveRebaseAction.Drop);
        }

        private async void StartJobs(object _1, RoutedEventArgs _2)
        {
            var vm = DataContext as ViewModels.InteractiveRebase;
            if (vm == null)
                return;

            Running.IsVisible = true;
            Running.IsIndeterminate = true;
            await vm.Start();
            Running.IsIndeterminate = false;
            Running.IsVisible = false;
            Close();
        }
    }
}
