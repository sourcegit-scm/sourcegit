using System;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class InitGit : UserControl
    {
        public InitGit()
        {
            InitializeComponent();
        }

        protected override async void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
        }

        private async void SelectParentFolder(object _, RoutedEventArgs e)
        {
            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            var toplevel = TopLevel.GetTopLevel(this);
            if (toplevel == null)
                return;

            try
            {
                var selected = await toplevel.StorageProvider.OpenFolderPickerAsync(options);
                if (selected.Count == 1)
                {
                    var folder = selected[0];
                    var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
                    TxtParentFolder.Text = folderPath;
                }
            }
            catch (Exception exception)
            {
                Models.Notification.Send(null, $"Failed to select parent folder: {exception.Message}", true);
            }

            e.Handled = true;
        }
    }
}
