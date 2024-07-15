using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class AddRemote : UserControl
    {
        public AddRemote()
        {
            InitializeComponent();
        }

        private async void SelectSSHKey(object _, RoutedEventArgs e)
        {
            var toplevel = TopLevel.GetTopLevel(this);
            if (toplevel == null)
                return;

            var options = new FilePickerOpenOptions()
            {
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("SSHKey") { Patterns = ["*.*"] }]
            };

            var selected = await toplevel.StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1)
                TxtSshKey.Text = selected[0].Path.LocalPath;

            e.Handled = true;
        }
    }
}
