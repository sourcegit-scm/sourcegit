using System;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class ExecuteCustomAction : UserControl
    {
        public ExecuteCustomAction()
        {
            InitializeComponent();
        }

        private async void SelectPath(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var control = sender as Control;
            if (control == null)
                return;

            var selector = control.DataContext as ViewModels.CustomActionControlPathSelector;
            if (selector == null)
                return;

            if (selector.IsFolder)
            {
                try
                {
                    var options = new FolderPickerOpenOptions() { AllowMultiple = false };
                    var selected = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
                    if (selected.Count == 1)
                    {
                        var folder = selected[0];
                        var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
                        selector.Path = folderPath;
                    }
                }
                catch (Exception exception)
                {
                    App.RaiseException(string.Empty, $"Failed to select parent folder: {exception.Message}");
                }
            }
            else
            {
                var options = new FilePickerOpenOptions()
                {
                    AllowMultiple = false,
                    FileTypeFilter = [new FilePickerFileType("File") { Patterns = ["*.*"] }]
                };

                var selected = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                if (selected.Count == 1)
                    selector.Path = selected[0].Path.LocalPath;
            }

            e.Handled = true;
        }
    }
}
