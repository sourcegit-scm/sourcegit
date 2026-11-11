using System;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public partial class OpenLocalRepository : UserControl
    {
        public OpenLocalRepository()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (_autoPickerShown)
                return;

            _autoPickerShown = true;
            if (DataContext is ViewModels.OpenLocalRepository { RepoPath: null or "" })
                Dispatcher.UIThread.Post(async () => await SelectRepositoryFolderAsync(), DispatcherPriority.Background);
        }

        private async void OnSelectRepositoryFolder(object _1, RoutedEventArgs e)
        {
            e.Handled = true;
            await SelectRepositoryFolderAsync();
        }

        private async Task SelectRepositoryFolderAsync()
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
        }

        private bool _autoPickerShown = false;
    }
}
