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

        private async void SelectLocation(object sender, RoutedEventArgs e)
        {
            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            var toplevel = TopLevel.GetTopLevel(this);
            if (toplevel == null)
                return;

            var selected = await toplevel.StorageProvider.OpenFolderPickerAsync(options);
            if (selected.Count == 1)
                TxtLocation.Text = selected[0].Path.LocalPath;

            e.Handled = true;
        }
    }
}
