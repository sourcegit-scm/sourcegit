using System;
using System.Collections.Generic;
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
            if (DataContext is ViewModels.Compare { ChangeSelection: { Count: > 0 } selection } vm)
            {
                var patch = new MenuItem();
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Icon = this.CreateMenuIcon("Icons.Save");
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
                            var succ = await vm.SaveChangesAsPatchAsync(selection.Changes, saveTo);
                            if (succ)
                                await new Alert().ShowAsync(this, "Save patch successfully.", false);
                        }
                    }
                    catch (Exception exception)
                    {
                        await new Alert().ShowAsync(this, $"Failed to save as patch: {exception.Message}", true);
                    }

                    e.Handled = true;
                };

                var selectedFolder = selection.IsSingleFolder;
                var fullPathOfFolder = selectedFolder ? vm.GetAbsPath(selection.SingleFolderPath) : null;
                var relativePathOfFolder = selectedFolder ? selection.SingleFolderPath : null;

                var menu = new ContextMenu();
                if (selection.Count == 1)
                {
                    var change = selection.Changes[0];
                    var changeFullPath = vm.GetAbsPath(change.Path);

                    var openWithMerger = new MenuItem();
                    openWithMerger.Header = App.Text("OpenInExternalMergeTool");
                    openWithMerger.Icon = this.CreateMenuIcon("Icons.OpenWith");
                    openWithMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
                    openWithMerger.IsVisible = !selectedFolder;
                    openWithMerger.Click += (_, ev) =>
                    {
                        vm.OpenInExternalDiffTool(change);
                        ev.Handled = true;
                    };

                    var explore = new MenuItem();
                    explore.Header = App.Text("RevealFile");
                    explore.Icon = this.CreateMenuIcon("Icons.Explore");
                    explore.IsEnabled = selectedFolder ? Directory.Exists(fullPathOfFolder) : File.Exists(changeFullPath);
                    explore.Click += (_, ev) =>
                    {
                        Native.OS.OpenInFileManager(selectedFolder ? fullPathOfFolder : changeFullPath);
                        ev.Handled = true;
                    };

                    menu.Items.Add(openWithMerger);
                    menu.Items.Add(explore);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(patch);

                    if (vm.CanResetFiles)
                    {
                        var resetToLeft = new MenuItem();
                        resetToLeft.Header = App.Text("ChangeCM.ResetFileTo", vm.BaseName);
                        resetToLeft.Icon = this.CreateMenuIcon("Icons.File.Checkout");
                        resetToLeft.Click += async (_, ev) =>
                        {
                            await vm.ResetToLeftAsync(change);
                            ev.Handled = true;
                        };

                        var resetToRight = new MenuItem();
                        resetToRight.Header = App.Text("ChangeCM.ResetFileTo", vm.ToName);
                        resetToRight.Icon = this.CreateMenuIcon("Icons.File.Checkout");
                        resetToRight.Click += async (_, ev) =>
                        {
                            await vm.ResetToRightAsync(change);
                            ev.Handled = true;
                        };

                        menu.Items.Add(new MenuItem() { Header = "-" });
                        menu.Items.Add(resetToLeft);
                        menu.Items.Add(resetToRight);
                    }

                    var copyPath = new MenuItem();
                    copyPath.Header = App.Text("CopyPath");
                    copyPath.Icon = this.CreateMenuIcon("Icons.Copy");
                    copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                    copyPath.Click += async (_, ev) =>
                    {
                        await this.CopyTextAsync(selectedFolder ? relativePathOfFolder : change.Path);
                        ev.Handled = true;
                    };

                    var copyFullPath = new MenuItem();
                    copyFullPath.Header = App.Text("CopyFullPath");
                    copyFullPath.Icon = this.CreateMenuIcon("Icons.Copy");
                    copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                    copyFullPath.Click += async (_, ev) =>
                    {
                        await this.CopyTextAsync(selectedFolder ? fullPathOfFolder : changeFullPath);
                        ev.Handled = true;
                    };

                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(copyPath);
                    menu.Items.Add(copyFullPath);
                }
                else
                {
                    if (selectedFolder)
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

                    menu.Items.Add(patch);

                    if (vm.CanResetFiles)
                    {
                        var resetToLeft = new MenuItem();
                        resetToLeft.Header = App.Text("ChangeCM.ResetFileTo", vm.BaseName);
                        resetToLeft.Icon = this.CreateMenuIcon("Icons.File.Checkout");
                        resetToLeft.Click += async (_, ev) =>
                        {
                            await vm.ResetMultipleToLeftAsync(selection.Changes);
                            ev.Handled = true;
                        };

                        var resetToRight = new MenuItem();
                        resetToRight.Header = App.Text("ChangeCM.ResetFileTo", vm.ToName);
                        resetToRight.Icon = this.CreateMenuIcon("Icons.File.Checkout");
                        resetToRight.Click += async (_, ev) =>
                        {
                            await vm.ResetMultipleToRightAsync(selection.Changes);
                            ev.Handled = true;
                        };

                        menu.Items.Add(new MenuItem() { Header = "-" });
                        menu.Items.Add(resetToLeft);
                        menu.Items.Add(resetToRight);
                    }

                    var copyPath = new MenuItem();
                    copyPath.Header = App.Text("CopyPath");
                    copyPath.Icon = this.CreateMenuIcon("Icons.Copy");
                    copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                    copyPath.Click += async (_, ev) =>
                    {
                        if (selectedFolder)
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
                        if (selectedFolder)
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

                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(copyPath);
                    menu.Items.Add(copyFullPath);
                }

                menu.Open(sender as Control);
            }

            e.Handled = true;
        }

        private void OnCommitListContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is not ViewModels.Compare vm)
                return;

            if (sender is ListBox { SelectedItems: { Count: > 0 } selected } listBox)
            {
                var commits = new List<Models.Commit>();
                foreach (var o in selected)
                {
                    if (o is Models.Commit c)
                        commits.Add(c);
                }

                if (commits.Count == 0)
                    return;

                commits.Sort((l, r) => l.CommitterTime.CompareTo(r.CommitterTime));

                var menu = new ContextMenu();
                var hasCurrentHead = vm.BaseHead.IsCurrentHead || vm.ToHead.IsCurrentHead;
                if (hasCurrentHead && listBox.Tag is Models.Commit { IsCurrentHead: false })
                {
                    var cherryPick = new MenuItem();
                    cherryPick.Header = App.Text("CommitCM.CherryPickMultiple");
                    cherryPick.Icon = this.CreateMenuIcon("Icons.CherryPick");
                    cherryPick.Click += (_, ev) =>
                    {
                        vm.CherryPick(commits);
                        ev.Handled = true;
                    };

                    menu.Items.Add(cherryPick);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                var copy = new MenuItem();
                copy.Header = App.Text("Copy");
                copy.Icon = this.CreateMenuIcon("Icons.Copy");
                copy.Click += async (_, ev) =>
                {
                    var builder = new StringBuilder();
                    foreach (var c in commits)
                        builder.Append(c.SHA.Substring(0, 10)).Append(" - ").AppendLine(c.Subject);

                    await this.CopyTextAsync(builder.ToString());
                    ev.Handled = true;
                };

                menu.Items.Add(copy);
                menu.Open(listBox);
                e.Handled = true;
            }
        }

        private void OnCommitListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.Compare vm &&
                sender is ListBox { SelectedItems: { Count: 1 } selected } listBox &&
                selected[0] is Models.Commit c)
            {
                vm.NavigateTo(c.SHA);
                e.Handled = true;
            }
        }

        private void OnPressedSHA(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is ViewModels.Compare vm && sender is TextBlock block)
                vm.NavigateTo(block.Text);

            e.Handled = true;
        }

        private async void OnChangeCollectionViewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.Compare { ChangeSelection: { Count: > 0 } selection } vm)
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
                ChangeSearchBox.Focus();
                e.Handled = true;
            }
        }
    }
}
