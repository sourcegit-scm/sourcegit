using System;
using System.IO;
using System.Text;

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
            if (DataContext is ViewModels.RevisionCompare { ChangeSelection: { Count: > 0 } selection } vm)
            {
                var patch = new MenuItem();
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Icon = this.CreateMenuIcon("Icons.Save");
                patch.Click += async (_, e) =>
                {
                    var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
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
                            await vm.SaveChangesAsPatchAsync(selection.Changes, saveTo);
                        }
                    }
                    catch (Exception exception)
                    {
                        Models.Notification.Send(null, $"Failed to save as patch: {exception.Message}", true);
                    }

                    e.Handled = true;
                };

                var selectedSingleFolder = selection.IsSingleFolder;
                var fullPathOfFolder = selectedSingleFolder ? vm.GetAbsPath(selection.SingleFolderPath) : null;
                var relativePathOfFolder = selectedSingleFolder ? selection.SingleFolderPath : null;

                var menu = new ContextMenu();
                if (selection.Count == 1)
                {
                    var change = selection.Changes[0];
                    var changeFullPath = vm.GetAbsPath(change.Path);

                    if (!selection.HasFolder)
                    {
                        var openWithMerger = new MenuItem();
                        openWithMerger.Header = App.Text("OpenInExternalMergeTool");
                        openWithMerger.Icon = this.CreateMenuIcon("Icons.OpenWith");
                        openWithMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
                        openWithMerger.Click += (_, ev) =>
                        {
                            vm.OpenChangeWithExternalDiffTool(change);
                            ev.Handled = true;
                        };
                        menu.Items.Add(openWithMerger);
                    }

                    if (!selection.HasFolder || selectedSingleFolder)
                    {
                        var explore = new MenuItem();
                        explore.Header = App.Text("RevealFile");
                        explore.Icon = this.CreateMenuIcon("Icons.Explore");
                        explore.IsEnabled = selectedSingleFolder ? Directory.Exists(fullPathOfFolder) : File.Exists(changeFullPath);
                        explore.Click += (_, ev) =>
                        {
                            Native.OS.OpenInFileManager(selectedSingleFolder ? fullPathOfFolder : changeFullPath);
                            ev.Handled = true;
                        };
                        menu.Items.Add(explore);
                    }

                    if (menu.Items.Count > 0)
                        menu.Items.Add(new MenuItem() { Header = "-" });

                    var resetToLeft = new MenuItem();
                    resetToLeft.Header = App.Text("ChangeCM.ResetFileTo", vm.LeftSideDesc);
                    resetToLeft.Icon = this.CreateMenuIcon("Icons.File.Checkout");
                    resetToLeft.IsEnabled = vm.CanResetToLeft;
                    resetToLeft.Click += async (_, ev) =>
                    {
                        await vm.ResetToLeftAsync(change);
                        ev.Handled = true;
                    };

                    var resetToRight = new MenuItem();
                    resetToRight.Header = App.Text("ChangeCM.ResetFileTo", vm.RightSideDesc);
                    resetToRight.Icon = this.CreateMenuIcon("Icons.File.Checkout");
                    resetToRight.IsEnabled = vm.CanResetToRight;
                    resetToRight.Click += async (_, ev) =>
                    {
                        await vm.ResetToRightAsync(change);
                        ev.Handled = true;
                    };

                    var copyPath = new MenuItem();
                    copyPath.Header = App.Text("CopyPath");
                    copyPath.Icon = this.CreateMenuIcon("Icons.Copy");
                    copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                    copyPath.Click += async (_, ev) =>
                    {
                        await this.CopyTextAsync(selectedSingleFolder ? relativePathOfFolder : change.Path);
                        ev.Handled = true;
                    };

                    var copyFullPath = new MenuItem();
                    copyFullPath.Header = App.Text("CopyFullPath");
                    copyFullPath.Icon = this.CreateMenuIcon("Icons.Copy");
                    copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                    copyFullPath.Click += async (_, ev) =>
                    {
                        await this.CopyTextAsync(selectedSingleFolder ? fullPathOfFolder : changeFullPath);
                        ev.Handled = true;
                    };

                    menu.Items.Add(patch);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(resetToLeft);
                    menu.Items.Add(resetToRight);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(copyPath);
                    menu.Items.Add(copyFullPath);
                }
                else
                {
                    if (selectedSingleFolder)
                    {
                        var explore = new MenuItem();
                        explore.Header = App.Text("RevealFile");
                        explore.Icon = this.CreateMenuIcon("Icons.Explore");
                        explore.IsEnabled = Directory.Exists(fullPathOfFolder);
                        explore.Click += (_, ev) =>
                        {
                            Native.OS.OpenInFileManager(fullPathOfFolder);
                            ev.Handled = true;
                        };

                        menu.Items.Add(explore);
                        menu.Items.Add(new MenuItem() { Header = "-" });
                    }

                    var resetToLeft = new MenuItem();
                    resetToLeft.Header = App.Text("ChangeCM.ResetFileTo", vm.LeftSideDesc);
                    resetToLeft.Icon = this.CreateMenuIcon("Icons.File.Checkout");
                    resetToLeft.IsEnabled = vm.CanResetToLeft;
                    resetToLeft.Click += async (_, ev) =>
                    {
                        await vm.ResetMultipleToLeftAsync(selection.Changes);
                        ev.Handled = true;
                    };

                    var resetToRight = new MenuItem();
                    resetToRight.Header = App.Text("ChangeCM.ResetFileTo", vm.RightSideDesc);
                    resetToRight.Icon = this.CreateMenuIcon("Icons.File.Checkout");
                    resetToRight.IsEnabled = vm.CanResetToRight;
                    resetToRight.Click += async (_, ev) =>
                    {
                        await vm.ResetMultipleToRightAsync(selection.Changes);
                        ev.Handled = true;
                    };

                    var copyPath = new MenuItem();
                    copyPath.Header = App.Text("CopyPath");
                    copyPath.Icon = this.CreateMenuIcon("Icons.Copy");
                    copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                    copyPath.Click += async (_, ev) =>
                    {
                        if (selectedSingleFolder)
                        {
                            await this.CopyTextAsync(relativePathOfFolder);
                        }
                        else
                        {
                            var builder = new StringBuilder();
                            foreach (var c in selection.Changes)
                                builder.AppendLine(c.Path);

                            await this.CopyTextAsync(builder.ToString());
                        }

                        ev.Handled = true;
                    };

                    var copyFullPath = new MenuItem();
                    copyFullPath.Header = App.Text("CopyFullPath");
                    copyFullPath.Icon = this.CreateMenuIcon("Icons.Copy");
                    copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                    copyFullPath.Click += async (_, ev) =>
                    {
                        if (selectedSingleFolder)
                        {
                            await this.CopyTextAsync(fullPathOfFolder);
                        }
                        else
                        {
                            var builder = new StringBuilder();
                            foreach (var c in selection.Changes)
                                builder.AppendLine(vm.GetAbsPath(c.Path));

                            await this.CopyTextAsync(builder.ToString());
                        }

                        ev.Handled = true;
                    };

                    menu.Items.Add(patch);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(resetToLeft);
                    menu.Items.Add(resetToRight);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(copyPath);
                    menu.Items.Add(copyFullPath);
                }

                menu.Open(sender as Control);
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
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage == null)
                return;

            if (DataContext is not ViewModels.RevisionCompare vm)
                return;

            var options = new FilePickerSaveOptions();
            options.Title = App.Text("FileCM.SaveAsPatch");
            options.DefaultExtension = ".patch";
            options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

            try
            {
                var storageFile = await storage.SaveFilePickerAsync(options);
                if (storageFile != null)
                    await vm.SaveChangesAsPatchAsync(null, storageFile.Path.LocalPath);
            }
            catch (Exception exception)
            {
                Models.Notification.Send(null, $"Failed to save as patch: {exception.Message}", true);
            }

            e.Handled = true;
        }

        private async void OnChangeCollectionViewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.RevisionCompare { ChangeSelection: { Count: > 0 } selection } vm)
                return;

            var cmdKey = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
            if (e.Key == Key.C && e.KeyModifiers.HasFlag(cmdKey))
            {
                var builder = new StringBuilder();
                var copyAbsPath = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                if (selection.IsSingleFolder)
                {
                    builder.Append(copyAbsPath ? vm.GetAbsPath(selection.SingleFolderPath) : selection.SingleFolderPath);
                }
                else if (selection.Changes.Count == 1)
                {
                    builder.Append(copyAbsPath ? vm.GetAbsPath(selection.Changes[0].Path) : selection.Changes[0].Path);
                }
                else
                {
                    foreach (var c in selection.Changes)
                        builder.AppendLine(copyAbsPath ? vm.GetAbsPath(c.Path) : c.Path);
                }

                await this.CopyTextAsync(builder.ToString());
                e.Handled = true;
            }
            else if (e.Key == Key.F && e.KeyModifiers == cmdKey)
            {
                RevisionCompareChangeSearchBox.Focus();
                e.Handled = true;
            }
        }
    }
}
