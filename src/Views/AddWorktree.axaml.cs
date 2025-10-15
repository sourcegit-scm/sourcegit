using System;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class AddWorktree : UserControl
    {
        public AddWorktree()
        {
            InitializeComponent();
        }

        private async void SelectLocation(object _, RoutedEventArgs e)
        {
            var toplevel = TopLevel.GetTopLevel(this);
            if (toplevel == null)
                return;

            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            try
            {
                var selected = await toplevel.StorageProvider.OpenFolderPickerAsync(options);
                if (selected.Count == 1)
                {
                    var folder = selected[0];
                    var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder.Path.ToString();
                    TxtLocation.Text = folderPath.TrimEnd('\\', '/');
                }
            }
            catch (Exception exception)
            {
                App.RaiseException(string.Empty, $"Failed to select location: {exception.Message}");
            }

            e.Handled = true;
        }
    }
}
