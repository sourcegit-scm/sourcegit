using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views {
    public partial class Apply : UserControl {
        public Apply() {
            InitializeComponent();
        }

        private async void SelectPatchFile(object sender, RoutedEventArgs e) {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var options = new FilePickerOpenOptions() { AllowMultiple = false, FileTypeFilter = [ new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }] };
            var selected = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1) {
                txtPatchFile.Text = selected[0].Path.LocalPath;
            }

            e.Handled = true;
        }
    }
}
