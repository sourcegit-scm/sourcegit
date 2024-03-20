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

        private async void SelectSSHKey(object sender, RoutedEventArgs e)
        {
            var options = new FilePickerOpenOptions() { AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("SSHKey") { Patterns = ["*.*"] }] };
            var toplevel = TopLevel.GetTopLevel(this);
            var selected = await toplevel.StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1)
            {
                txtSSHKey.Text = selected[0].Path.LocalPath;
            }

            e.Handled = true;
        }
    }
}