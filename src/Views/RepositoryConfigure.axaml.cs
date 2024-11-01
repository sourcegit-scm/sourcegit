using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class RepositoryConfigure : ChromelessWindow
    {
        public RepositoryConfigure()
        {
            InitializeComponent();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            (DataContext as ViewModels.RepositoryConfigure)?.Save();
            base.OnClosing(e);
        }

        private async void SelectExecutableForCustomAction(object sender, RoutedEventArgs e)
        {
            var options = new FilePickerOpenOptions()
            {
                FileTypeFilter = [new FilePickerFileType("Executable file(script)") { Patterns = ["*.*"] }],
                AllowMultiple = false,
            };

            var selected = await StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1 && sender is Button { DataContext: Models.CustomAction action })
                action.Executable = selected[0].Path.LocalPath;

            e.Handled = true;
        }
    }
}
