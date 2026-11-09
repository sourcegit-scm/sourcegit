using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
                FileTypeFilter = [new FilePickerFileType("Patch File") { Patterns = ["*.patch", "*.diff"] }]
            };

            var selected = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1)
                TxtPatchFile.Text = selected[0].Path.LocalPath;

            e.Handled = true;
        }

        private void DragOverPatchFileBox(object sender, DragEventArgs e)
        {
            if (!e.DataTransfer.Contains(DataFormat.File))
            {
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var files = e.DataTransfer.TryGetFiles();
            if (files is not { Length: 1 })
            {
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var file = files[0].Path.LocalPath;
            if (!file.EndsWith(".patch") && !file.EndsWith(".diff"))
            {
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        private void DropOnPatchFileBox(object sender, DragEventArgs e)
        {
            if (!e.DataTransfer.Contains(DataFormat.File))
                return;

            var files = e.DataTransfer.TryGetFiles();
            if (files is not { Length: 1 })
                return;

            var file = files[0].Path.LocalPath;
            if (!file.EndsWith(".patch") && !file.EndsWith(".diff"))
                return;

            TxtPatchFile.Text = file;
            e.Handled = true;
        }
    }
}
