using System;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

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

        private async void OnResetToSelectedRevision(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: ViewModels.FileHistoriesSingleRevision single })
            {
                await single.ResetToSelectedRevisionAsync();
                NotifyDonePanel.IsVisible = true;
            }

            e.Handled = true;
        }

        private void OnCloseNotifyPanel(object _, PointerPressedEventArgs e)
        {
            NotifyDonePanel.IsVisible = false;
            e.Handled = true;
        }

        private async void OnSaveAsPatch(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: ViewModels.FileHistoriesCompareRevisions compare })
            {
                var options = new FilePickerSaveOptions();
                options.Title = App.Text("FileCM.SaveAsPatch");
                options.DefaultExtension = ".patch";
                options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                try
                {
                    var storageFile = await StorageProvider.SaveFilePickerAsync(options);
                    if (storageFile != null)
                        await compare.SaveAsPatch(storageFile.Path.LocalPath);

                    NotifyDonePanel.IsVisible = true;
                }
                catch (Exception exception)
                {
                    App.RaiseException(string.Empty, $"Failed to save as patch: {exception.Message}");
                }

                e.Handled = true;
            }
        }

        private void OnCommitSubjectDataContextChanged(object sender, EventArgs e)
        {
            if (sender is Border border)
                ToolTip.SetTip(border, null);
        }

        private void OnCommitSubjectPointerMoved(object sender, PointerEventArgs e)
        {
            if (sender is Border { DataContext: Models.Commit commit } border &&
                DataContext is ViewModels.FileHistories vm)
            {
                var tooltip = ToolTip.GetTip(border);
                if (tooltip == null)
                    ToolTip.SetTip(border, vm.GetCommitFullMessage(commit));
            }
        }

        private async void OnOpenFileWithDefaultEditor(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.FileHistories { ViewContent: ViewModels.FileHistoriesSingleRevision revision })
                await revision.OpenWithDefaultEditorAsync();

            e.Handled = true;
        }
    }
}
