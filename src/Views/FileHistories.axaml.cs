using System;
using System.Collections.Generic;

using Avalonia;
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

        private void OnRevisionsPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ListBox.ItemsSourceProperty &&
                sender is ListBox { Items: { Count: > 0 } } listBox)
                listBox.SelectedIndex = 0;
        }

        private void OnRevisionsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && DataContext is ViewModels.FileHistories vm)
            {
                if (listBox.SelectedItems is { } selected)
                {
                    var revs = new List<Models.FileVersion>();
                    foreach (var item in listBox.SelectedItems)
                    {
                        if (item is Models.FileVersion ver)
                            revs.Add(ver);
                    }
                    vm.SelectedRevisions = revs;
                }
                else
                {
                    vm.SelectedRevisions = [];
                }
            }
        }

        private void OnPressCommitSHA(object sender, PointerPressedEventArgs e)
        {
            if (sender is TextBlock { DataContext: Models.FileVersion ver } &&
                DataContext is ViewModels.FileHistories vm)
            {
                vm.NavigateToCommit(ver);
            }

            e.Handled = true;
        }

        private async void OnResetToSelectedRevision(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: ViewModels.FileHistoriesSingleRevision single })
            {
                await single.ResetToSelectedRevisionAsync();
                await new Alert().ShowAsync(this, "Reset to selected revision successfully.", false);
            }

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
                    if (storageFile == null)
                        return;

                    var succ = await compare.SaveAsPatch(storageFile.Path.LocalPath);
                    if (succ)
                        await new Alert().ShowAsync(this, "Saved as patch successfully.", false);
                }
                catch (Exception exception)
                {
                    await new Alert().ShowAsync(this, $"Failed to save as patch: {exception.Message}", true);
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
            if (sender is Border { DataContext: Models.FileVersion ver } border &&
                DataContext is ViewModels.FileHistories vm)
            {
                var tooltip = ToolTip.GetTip(border);
                if (tooltip == null)
                    ToolTip.SetTip(border, vm.GetCommitFullMessage(ver));
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
