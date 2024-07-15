using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class Archive : UserControl
    {
        public Archive()
        {
            InitializeComponent();
        }

        private async void SelectOutputFile(object _, RoutedEventArgs e)
        {
            var toplevel = TopLevel.GetTopLevel(this);
            if (toplevel == null)
                return;

            var options = new FilePickerSaveOptions()
            {
                DefaultExtension = ".zip",
                FileTypeChoices = [new FilePickerFileType("ZIP") { Patterns = ["*.zip"] }]
            };

            var selected = await toplevel.StorageProvider.SaveFilePickerAsync(options);
            if (selected != null)
                TxtSaveFile.Text = selected.Path.LocalPath;

            e.Handled = true;
        }
    }
}
