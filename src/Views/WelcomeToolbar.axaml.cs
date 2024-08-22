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
            if (!ViewModels.PopupHost.CanCreatePopup())
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

            var selected = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (selected.Count == 1)
                OpenOrInitRepository(selected[0].Path.LocalPath);

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

            var root = new Commands.QueryRepositoryRootPath(path).Result();
            if (string.IsNullOrEmpty(root))
            {
                ViewModels.Welcome.Instance.InitRepository(path, parent);
                return;
            }

            var normalizedPath = root.Replace("\\", "/");
            var node = ViewModels.Preference.Instance.FindOrAddNodeByRepositoryPath(normalizedPath, parent, false);
            ViewModels.Welcome.Instance.Refresh();

            var launcher = this.FindAncestorOfType<Launcher>()?.DataContext as ViewModels.Launcher;
            launcher?.OpenRepositoryInTab(node, launcher.ActivePage);
        }
    }
}

