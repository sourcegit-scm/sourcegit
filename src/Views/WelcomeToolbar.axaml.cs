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
            var activePage = App.GetLauncer().ActivePage;
            if (activePage == null || !activePage.CanCreatePopup())
                return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            if (Directory.Exists(ViewModels.Preferences.Instance.GitDefaultCloneDir))
            {
                var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(ViewModels.Preferences.Instance.GitDefaultCloneDir);
                options.SuggestedStartLocation = folder;
            }

            try
            {
                var selected = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
                if (selected.Count == 1)
                    ViewModels.Welcome.Instance.OpenOrInitRepository(selected[0].Path.LocalPath, null, false);
            }
            catch (Exception exception)
            {
                App.RaiseException(string.Empty, $"Failed to open repository: {exception.Message}");
            }

            e.Handled = true;
        }
    }
}

