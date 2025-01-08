using System;
using System.IO;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

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
            var activePage = App.GetLauncer().ActivePage;
            if (activePage == null || !activePage.CanCreatePopup())
                return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            if (Directory.Exists(ViewModels.Preference.Instance.GitDefaultCloneDir))
            {
                var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(ViewModels.Preference.Instance.GitDefaultCloneDir);
                options.SuggestedStartLocation = folder;
            }

            try
            {
                var selected = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
                if (selected.Count == 1)
                    OpenOrInitRepository(selected[0].Path.LocalPath);
            }
            catch (Exception exception)
            {
                App.RaiseException(string.Empty, $"Failed to open repository: {exception.Message}");
            }

            e.Handled = true;
        }

        private void OpenOrInitRepository(string path, ViewModels.RepositoryNode parent = null)
        {
            if (!Directory.Exists(path))
            {
                if (File.Exists(path))
                    path = Path.GetDirectoryName(path);
                else
                    return;
            }

            var test = new Commands.QueryRepositoryRootPath(path).ReadToEnd();
            if (!test.IsSuccess || string.IsNullOrEmpty(test.StdOut))
            {
                ViewModels.Welcome.Instance.InitRepository(path, parent, test.StdErr);
                return;
            }

            var normalizedPath = test.StdOut.Trim().Replace("\\", "/");
            var node = ViewModels.Preference.Instance.FindOrAddNodeByRepositoryPath(normalizedPath, parent, false);
            ViewModels.Welcome.Instance.Refresh();

            var launcher = this.FindAncestorOfType<Launcher>()?.DataContext as ViewModels.Launcher;
            launcher?.OpenRepositoryInTab(node, launcher.ActivePage);
        }
    }
}

