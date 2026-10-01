using System;
using System.IO;

using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class Clone : UserControl
    {
        public Clone()
        {
            InitializeComponent();
        }

        protected override async void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (DataContext is not ViewModels.Clone vm)
                return;

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                try
                {
                    var text = await clipboard.TryGetTextAsync();
                    if (Models.Remote.IsValidURL(text))
                        vm.Remote = text;
                }
                catch
                {
                    // Ignore exceptions here.
                }
            }
        }

        private async void SelectParentFolder(object _, RoutedEventArgs e)
        {
            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            var toplevel = TopLevel.GetTopLevel(this);
            if (toplevel == null)
                return;

            var prefilled = TxtParentFolder.Text;
            if (!string.IsNullOrWhiteSpace(prefilled) && Directory.Exists(prefilled))
                options.SuggestedStartLocation = await toplevel.StorageProvider.TryGetFolderFromPathAsync(prefilled);

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

        private async void SelectSSHKey(object _, RoutedEventArgs e)
        {
            var toplevel = TopLevel.GetTopLevel(this);
            if (toplevel == null)
                return;

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var startupDir = Path.Combine(home, ".ssh");
            if (!Directory.Exists(startupDir))
                startupDir = home;

            var suggestedStartLocation = await toplevel.StorageProvider.TryGetFolderFromPathAsync(startupDir);
            var options = new FilePickerOpenOptions()
            {
                AllowMultiple = false,
                FileTypeFilter = [new("SSHKey") { Patterns = ["*"] }],
                SuggestedStartLocation = suggestedStartLocation
            };

            var selected = await toplevel.StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1)
                TxtSshKey.Text = selected[0].Path.LocalPath;

            e.Handled = true;
        }
    }
}
