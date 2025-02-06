using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SourceGit.Models;

namespace SourceGit.Views
{
    public partial class FileHistories : ChromelessWindow
    {
        public FileHistories()
        {
            InitializeComponent();
        }

        private void OnPressCommitSHA(object sender, PointerPressedEventArgs e)
        {
            if (sender is TextBlock { DataContext: Models.Commit commit } &&
                DataContext is ViewModels.FileHistories vm)
            {
                vm.NavigateToCommit(commit);
            }

            e.Handled = true;
        }

        private void OnResetToSelectedRevision(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.FileHistories vm)
            {
                vm.ResetToSelectedRevision();
                NotifyDonePanel.IsVisible = true;
            }

            e.Handled = true;
        }

        private void OnCloseNotifyPanel(object _, PointerPressedEventArgs e)
        {
            NotifyDonePanel.IsVisible = false;
            e.Handled = true;
        }

        private void OnRowSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.FileHistories vm && sender is ListBox { SelectedItems: IList<object> commits })
            {
                var selectedCommits = new List<Models.Commit>();
                foreach (var commit in commits)
                {
                    if (commit is Models.Commit modelCommit)
                    {
                        selectedCommits.Add(modelCommit);
                    }
                }
                vm.SelectedCommits = selectedCommits;
            }

            e.Handled = true;
        }

        private async void OnSaveAsPatch(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var vm = DataContext as ViewModels.FileHistories;
            if (vm == null)
                return;

            var options = new FilePickerSaveOptions();
            options.Title = App.Text("FileCM.SaveAsPatch");
            options.DefaultExtension = ".patch";
            options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

            var storageFile = await topLevel.StorageProvider.SaveFilePickerAsync(options);
            if (storageFile != null)
                await vm.SaveAsPatch(storageFile.Path.LocalPath);
            NotifyDonePanel.IsVisible = true;

            e.Handled = true;
        }
    }
}
