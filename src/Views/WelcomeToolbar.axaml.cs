using System;
using System.IO;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class WelcomeToolbar : UserControl
    {
        public WelcomeToolbar()
        {
            InitializeComponent();
        }

        private async void OpenLocalRepository(object _1, RoutedEventArgs e)
        {
            var activePage = App.GetLauncher().ActivePage;
            if (activePage == null || !activePage.CanCreatePopup())
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
                    var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
                    var repoPath = await ViewModels.Welcome.Instance.GetRepositoryRootAsync(folderPath);
                    if (!string.IsNullOrEmpty(repoPath))
                    {
                        ViewModels.Welcome.Instance.AddRepository(repoPath, null, false, true);
                        ViewModels.Welcome.Instance.Refresh();
                    }
                    else if (Directory.Exists(folderPath))
                    {
                        var test = await new Commands.QueryRepositoryRootPath(folderPath).GetResultAsync();
                        ViewModels.Welcome.Instance.InitRepository(folderPath, null, test.StdErr);
                    }
                }
            }
            catch (Exception exception)
            {
                App.RaiseException(string.Empty, $"Failed to open repository: {exception.Message}");
            }

            e.Handled = true;
        }
    }
}
