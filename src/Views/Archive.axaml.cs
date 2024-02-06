using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views {
    public partial class Archive : UserControl {
        public Archive() {
            InitializeComponent();
        }

        private async void SelectOutputFile(object sender, RoutedEventArgs e) {
            var options = new FilePickerSaveOptions() { DefaultExtension = ".zip", FileTypeChoices = [ new FilePickerFileType("ZIP") { Patterns = [ "*.zip" ]}] };
            var toplevel = TopLevel.GetTopLevel(this);
            var selected = await toplevel.StorageProvider.SaveFilePickerAsync(options);
            if (selected != null) {
                txtSaveFile.Text = selected.Path.LocalPath;
            }

            e.Handled = true;
        }
    }
}
