using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class ViewLogs : ChromelessWindow
    {
        public ViewLogs()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ViewLogs vm && vm.Logs.Count > 0)
                vm.SelectedLog = vm.Logs[0];
        }

        private void OnLogContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is not Grid { DataContext: ViewModels.CommandLog log } grid || DataContext is not ViewModels.ViewLogs vm)
                return;

            var copy = new MenuItem();
            copy.Header = App.Text("ViewLogs.CopyLog");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, ev) =>
            {
                await App.CopyTextAsync(log.Content);
                ev.Handled = true;
            };

            var rm = new MenuItem();
            rm.Header = App.Text("ViewLogs.Delete");
            rm.Icon = App.CreateMenuIcon("Icons.Clear");
            rm.Click += (_, ev) =>
            {
                vm.Logs.Remove(log);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(copy);
            menu.Items.Add(rm);
            menu.Open(grid);

            e.Handled = true;
        }

        private void OnLogKeyDown(object _, KeyEventArgs e)
        {
            if (e.Key is not (Key.Delete or Key.Back))
                return;

            if (DataContext is ViewModels.ViewLogs { SelectedLog: { } log } vm)
                vm.Logs.Remove(log);

            e.Handled = true;
        }
    }
}
