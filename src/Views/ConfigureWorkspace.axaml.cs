using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class ConfigureWorkspace : ChromelessWindow
    {
        public ConfigureWorkspace()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (!Design.IsDesignMode)
                ViewModels.Preferences.Instance.Save();
        }

        private async void SelectDefaultCloneDir(object _, RoutedEventArgs e)
        {
            var workspace = DataContext as ViewModels.ConfigureWorkspace;
            if (workspace?.Selected == null)
                return;

            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            try
            {
                var selected = await StorageProvider.OpenFolderPickerAsync(options);
                if (selected.Count == 1)
                {
                    var folder = selected[0];
                    var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
                    workspace.Selected.DefaultCloneDir = folderPath;
                }
            }
            catch (Exception ex)
            {
                App.RaiseException(string.Empty, $"Failed to select default clone directory: {ex.Message}");
            }

            e.Handled = true;
        }
    }
}
