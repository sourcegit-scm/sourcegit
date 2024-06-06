using Avalonia.Controls;
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

        private async void SelectParentFolder(object sender, RoutedEventArgs e)
        {
            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            var toplevel = TopLevel.GetTopLevel(this);
            if (toplevel == null)
                return;

            var selected = await toplevel.StorageProvider.OpenFolderPickerAsync(options);
            if (selected.Count == 1)
                TxtParentFolder.Text = selected[0].Path.LocalPath;

            e.Handled = true;
        }

        private async void SelectSSHKey(object sender, RoutedEventArgs e)
        {
            var options = new FilePickerOpenOptions() { AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("SSHKey") { Patterns = ["*.*"] }] };
            var toplevel = TopLevel.GetTopLevel(this);
            if (toplevel == null)
                return;

            var selected = await toplevel.StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1)
                TxtSshKey.Text = selected[0].Path.LocalPath;

            e.Handled = true;
        }
    }
}
