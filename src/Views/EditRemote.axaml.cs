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

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            if (DataContext is ViewModels.EditRemote vm)
                vm.Load();
        }

        private async void SelectSSHKey(object _, RoutedEventArgs e)
        {
            var toplevel = TopLevel.GetTopLevel(this);
            if (toplevel == null)
                return;

            var options = new FilePickerOpenOptions() { AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("SSHKey") { Patterns = ["*.*"] }] };
            var selected = await toplevel.StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1)
                TxtSshKey.Text = selected[0].Path.LocalPath;

            e.Handled = true;
        }
    }
}
