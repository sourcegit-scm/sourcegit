using System;
using System.IO;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class OpenLocalRepository : UserControl
    {
        public OpenLocalRepository()
        {
            InitializeComponent();
        }

        private async void OnSelectRepositoryFolder(object _1, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.OpenLocalRepository vm)
                return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var preference = ViewModels.Preferences.Instance;
            var workspace = preference.GetActiveWorkspace();
            var initDir = workspace.DefaultCloneDir;
            if (string.IsNullOrEmpty(initDir) || !Directory.Exists(initDir))
                initDir = preference.GitDefaultCloneDir;

            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            if (Directory.Exists(initDir))
            {
                var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initDir);
                options.SuggestedStartLocation = folder;
            }

            try
            {
                var selected = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
                if (selected.Count == 1)
                {
                    var folder = selected[0];
                    vm.RepoPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
                }
            }
            catch (Exception exception)
            {
                Models.Notification.Send(null, $"Failed to open repository: {exception.Message}", true);
            }

            e.Handled = true;
        }
    }
}
