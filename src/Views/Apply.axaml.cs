using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class Apply : UserControl
    {
        public Apply()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (DataContext is ViewModels.Apply vm && TopLevel.GetTopLevel(this) is { } toplevel)
                vm.Clipboard = toplevel.Clipboard;
        }

        private async void SelectPatchFile(object _, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var options = new FilePickerOpenOptions()
            {
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }]
            };

            var selected = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1)
                TxtPatchFile.Text = selected[0].Path.LocalPath;

            e.Handled = true;
        }
    }
}
