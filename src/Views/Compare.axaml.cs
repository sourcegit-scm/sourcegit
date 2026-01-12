using System;
using System.IO;
using System.Text;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class Compare : ChromelessWindow
    {
        public Compare()
        {
            InitializeComponent();
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.Compare { SelectedChanges: { Count: > 0 } selected } vm &&
                sender is ChangeCollectionView view)
            {
                var menu = new ContextMenu();

                var patch = new MenuItem();
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Icon = App.CreateMenuIcon("Icons.Diff");
                patch.Click += async (_, e) =>
                {
                    var storageProvider = this.StorageProvider;
                    if (storageProvider == null)
                        return;

                    var options = new FilePickerSaveOptions();
                    options.Title = App.Text("FileCM.SaveAsPatch");
                    options.DefaultExtension = ".patch";
                    options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                    try
                    {
                        var storageFile = await storageProvider.SaveFilePickerAsync(options);
                        if (storageFile != null)
                        {
                            var saveTo = storageFile.Path.LocalPath;
                            await vm.SaveChangesAsPatchAsync(selected, saveTo);
                        }
                    }
                    catch (Exception exception)
                    {
                        App.RaiseException(null, $"Failed to save as patch: {exception.Message}");
                    }

                    e.Handled = true;
                };

                if (selected.Count == 1)
                {
                    var change = selected[0];
                    var openWithMerger = new MenuItem();
                    openWithMerger.Header = App.Text("OpenInExternalMergeTool");
                    openWithMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
                    openWithMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
                    openWithMerger.Click += (_, ev) =>
                    {
                        vm.OpenInExternalDiffTool(change);
                        ev.Handled = true;
                    };
                    menu.Items.Add(openWithMerger);

                    if (change.Index != Models.ChangeState.Deleted)
                    {
                        var full = vm.GetAbsPath(change.Path);
                        var explore = new MenuItem();
                        explore.Header = App.Text("RevealFile");
                        explore.Icon = App.CreateMenuIcon("Icons.Explore");
                        explore.IsEnabled = File.Exists(full);
                        explore.Click += (_, ev) =>
                        {
                            Native.OS.OpenInFileManager(full, true);
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

                    var copyFullPath = new MenuItem();
                    copyFullPath.Header = App.Text("CopyFullPath");
                    copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
                    copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                    copyFullPath.Click += async (_, ev) =>
                    {
                        await App.CopyTextAsync(vm.GetAbsPath(change.Path));
                        ev.Handled = true;
                    };

                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(patch);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(copyPath);
                    menu.Items.Add(copyFullPath);
                }
                else
                {
                    var copyPath = new MenuItem();
                    copyPath.Header = App.Text("CopyPath");
                    copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
                    copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                    copyPath.Click += async (_, ev) =>
                    {
                        var builder = new StringBuilder();
                        foreach (var c in selected)
                            builder.AppendLine(c.Path);

                        await App.CopyTextAsync(builder.ToString());
                        ev.Handled = true;
                    };

                    var copyFullPath = new MenuItem();
                    copyFullPath.Header = App.Text("CopyFullPath");
                    copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
                    copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                    copyFullPath.Click += async (_, ev) =>
                    {
                        var builder = new StringBuilder();
                        foreach (var c in selected)
                            builder.AppendLine(vm.GetAbsPath(c.Path));

                        await App.CopyTextAsync(builder.ToString());
                        ev.Handled = true;
                    };

                    menu.Items.Add(patch);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(copyPath);
                    menu.Items.Add(copyFullPath);
                }

                menu.Open(view);
            }

            e.Handled = true;
        }

        private void OnPressedSHA(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is ViewModels.Compare vm && sender is TextBlock block)
                vm.NavigateTo(block.Text);

            e.Handled = true;
        }

        private async void OnChangeCollectionViewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.Compare vm)
                return;

            if (sender is not ChangeCollectionView { SelectedChanges: { Count: > 0 } selectedChanges })
                return;

            if (e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) && e.Key == Key.C)
            {
                var builder = new StringBuilder();
                var copyAbsPath = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                if (selectedChanges.Count == 1)
                {
                    builder.Append(copyAbsPath ? vm.GetAbsPath(selectedChanges[0].Path) : selectedChanges[0].Path);
                }
                else
                {
                    foreach (var c in selectedChanges)
                        builder.AppendLine(copyAbsPath ? vm.GetAbsPath(c.Path) : c.Path);
                }

                await App.CopyTextAsync(builder.ToString());
                e.Handled = true;
            }
        }
    }
}
