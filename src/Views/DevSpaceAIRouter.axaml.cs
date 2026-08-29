using System;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class DevSpaceAIRouter : UserControl
    {
        public DevSpaceAIRouter()
        {
            InitializeComponent();
        }

        private ViewModels.DevSpaceAIRouter ViewModel => DataContext as ViewModels.DevSpaceAIRouter;

        private void OnAddProvider(object sender, RoutedEventArgs e)
        {
            ViewModel?.AddProvider();
            e.Handled = true;
        }

        private void OnDuplicate(object sender, RoutedEventArgs e)
        {
            ViewModel?.DuplicateSelected();
            e.Handled = true;
        }

        private void OnDelete(object sender, RoutedEventArgs e)
        {
            ViewModel?.DeleteSelected();
            e.Handled = true;
        }

        private async void OnSave(object sender, RoutedEventArgs e)
        {
            await RunAsync(() =>
            {
                ViewModel?.Save();
                return Task.CompletedTask;
            });
            e.Handled = true;
        }

        private async void OnTestConnection(object sender, RoutedEventArgs e)
        {
            await RunAsync(() => ViewModel?.TestSelectedAsync() ?? Task.CompletedTask);
            e.Handled = true;
        }

        private async void OnImport(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null || ViewModel == null)
                return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import AI Router Providers",
                AllowMultiple = false,
                FileTypeFilter = [JsonFileType],
            });
            if (files.Count == 0)
                return;

            await RunAsync(async () =>
            {
                await using var stream = await files[0].OpenReadAsync();
                await ViewModel.ImportAsync(stream);
            });
            e.Handled = true;
        }

        private async void OnExport(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null || ViewModel == null)
                return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export AI Router Providers",
                SuggestedFileName = "ai-router-providers.json",
                FileTypeChoices = [JsonFileType],
            });
            if (file == null)
                return;

            await RunAsync(async () =>
            {
                await using var stream = await file.OpenWriteAsync();
                await ViewModel.ExportAsync(stream);
            });
            e.Handled = true;
        }

        private async Task RunAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                if (TopLevel.GetTopLevel(this) is Window owner)
                    await new Alert().ShowAsync(owner, ex.Message, true);
            }
        }

        private static readonly FilePickerFileType JsonFileType = new("JSON")
        {
            Patterns = ["*.json"],
            MimeTypes = ["application/json"],
        };
    }
}
