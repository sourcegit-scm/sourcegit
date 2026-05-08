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

            if (control?.DataContext is not ViewModels.CustomActionControlPathSelector selector)
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
                    Models.Notification.Send(null, $"Failed to select parent folder: {exception.Message}", true);
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
