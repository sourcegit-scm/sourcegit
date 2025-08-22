using System;
using System.IO;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class RevisionCompare : UserControl
    {
        public RevisionCompare()
        {
            InitializeComponent();
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.RevisionCompare { SelectedChanges: { Count: 1 } selected } vm &&
                sender is ChangeCollectionView view)
            {
                var change = selected[0];
                var changeFullPath = vm.GetAbsPath(change.Path);
                var menu = new ContextMenu();

                var openWithMerger = new MenuItem();
                openWithMerger.Header = App.Text("OpenInExternalMergeTool");
                openWithMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
                openWithMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
                openWithMerger.Click += (_, ev) =>
                {
                    vm.OpenChangeWithExternalDiffTool(change);
                    ev.Handled = true;
                };
                menu.Items.Add(openWithMerger);

                if (change.Index != Models.ChangeState.Deleted)
                {
                    var explore = new MenuItem();
                    explore.Header = App.Text("RevealFile");
                    explore.Icon = App.CreateMenuIcon("Icons.Explore");
                    explore.IsEnabled = File.Exists(changeFullPath);
                    explore.Click += (_, ev) =>
                    {
                        Native.OS.OpenInFileManager(changeFullPath, true);
                        ev.Handled = true;
                    };
                    menu.Items.Add(explore);
                }

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("CopyPath");
                copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
                copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                copyPath.Click += async (_, ev) =>
                {
                    await App.CopyTextAsync(change.Path);
                    ev.Handled = true;
                };
                menu.Items.Add(copyPath);

                var copyFullPath = new MenuItem();
                copyFullPath.Header = App.Text("CopyFullPath");
                copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
                copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                copyFullPath.Click += async (_, ev) =>
                {
                    await App.CopyTextAsync(changeFullPath);
                    ev.Handled = true;
                };
                menu.Items.Add(copyFullPath);
                menu.Open(view);
            }

            e.Handled = true;
        }

        private void OnPressedSHA(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is ViewModels.RevisionCompare vm && sender is TextBlock block)
                vm.NavigateTo(block.Text);

            e.Handled = true;
        }

        private async void OnSaveAsPatch(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            if (DataContext is not ViewModels.RevisionCompare vm)
                return;

            var options = new FilePickerSaveOptions();
            options.Title = App.Text("FileCM.SaveAsPatch");
            options.DefaultExtension = ".patch";
            options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

            var storageFile = await topLevel.StorageProvider.SaveFilePickerAsync(options);
            if (storageFile != null)
                vm.SaveAsPatch(storageFile.Path.LocalPath);

            e.Handled = true;
        }

        private async void OnChangeCollectionViewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.RevisionCompare vm)
                return;

            if (sender is not ChangeCollectionView { SelectedChanges: { Count: 1 } selectedChanges })
                return;

            var change = selectedChanges[0];
            if (e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) && e.Key == Key.C)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    await App.CopyTextAsync(vm.GetAbsPath(change.Path));
                else
                    await App.CopyTextAsync(change.Path);

                e.Handled = true;
            }
        }
    }
}
