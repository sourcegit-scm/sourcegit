using System;
using System.IO;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class EditRemote : UserControl
    {
        public EditRemote()
        {
            InitializeComponent();
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
