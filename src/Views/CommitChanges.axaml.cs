using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class CommitChanges : UserControl
    {
        public CommitChanges()
        {
            InitializeComponent();
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            e.Handled = true;

            if (sender is not ChangeCollectionView { Selection: { Count: > 0 } selection } view)
                return;

            if (selection.IsSingleFolder)
                CreateChangeContextMenuByFolder(selection.SingleFolderPath, selection.Changes)?.Open(view);
            else if (selection.Changes.Count > 1)
                CreateMultipleChangesContextMenu(selection.Changes)?.Open(view);
            else
                this.FindAncestorOfType<CommitDetail>()?.CreateChangeContextMenu(selection.Changes[0])?.Open(view);
        }

        private async void OnChangeCollectionViewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.CommitDetail vm)
                return;

            if (sender is not ChangeCollectionView { Selection: { Count: > 0 } selection } view)
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
                CommitChangeSearchBox.Focus();
                e.Handled = true;
            }
        }

        private ContextMenu CreateChangeContextMenuByFolder(string folder, List<Models.Change> changes)
        {
            if (DataContext is not ViewModels.CommitDetail { Repository: { } repo, Commit: { } commit } vm)
                return null;

            var fullPath = Native.OS.GetAbsPath(repo.FullPath, folder);
            var explore = new MenuItem();
            explore.Header = App.Text("RevealFile");
            explore.Icon = this.CreateMenuIcon("Icons.Explore");
            explore.IsEnabled = Directory.Exists(fullPath);
            explore.Click += (_, ev) =>
            {
                Native.OS.OpenInFileManager(fullPath);
                ev.Handled = true;
            };

            var history = new MenuItem();
            history.Header = App.Text("DirHistories");
            history.Icon = this.CreateMenuIcon("Icons.Histories");
            history.Click += (_, ev) =>
            {
                this.ShowWindow(new ViewModels.DirHistories(repo, folder, commit.SHA));
                ev.Handled = true;
            };

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
                        await vm.SaveChangesAsPatchAsync(changes, saveTo);
                    }
                }
                catch (Exception exception)
                {
                    repo.SendNotification($"Failed to save as patch: {exception.Message}", true);
                }

                e.Handled = true;
            };

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Icon = this.CreateMenuIcon("Icons.Copy");
            copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyPath.Click += async (_, ev) =>
            {
                await this.CopyTextAsync(folder);
                ev.Handled = true;
            };

            var copyFullPath = new MenuItem();
            copyFullPath.Header = App.Text("CopyFullPath");
            copyFullPath.Icon = this.CreateMenuIcon("Icons.Copy");
            copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
            copyFullPath.Click += async (_, e) =>
            {
                await this.CopyTextAsync(fullPath);
                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(explore);
            menu.Items.Add(new MenuItem { Header = "-" });
            menu.Items.Add(history);
            menu.Items.Add(patch);
            menu.Items.Add(new MenuItem { Header = "-" });
            menu.Items.Add(copyPath);
            menu.Items.Add(copyFullPath);

            return menu;
        }

        private ContextMenu CreateMultipleChangesContextMenu(List<Models.Change> changes)
        {
            if (DataContext is not ViewModels.CommitDetail { Repository: { } repo, Commit: { } commit } vm)
                return null;

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
                        await vm.SaveChangesAsPatchAsync(changes, saveTo);
                    }
                }
                catch (Exception exception)
                {
                    repo.SendNotification($"Failed to save as patch: {exception.Message}", true);
                }

                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(patch);
            menu.Items.Add(new MenuItem() { Header = "-" });

            if (!repo.IsBare)
            {
                var resetToThisRevision = new MenuItem();
                resetToThisRevision.Header = App.Text("ChangeCM.CheckoutThisRevision");
                resetToThisRevision.Icon = this.CreateMenuIcon("Icons.File.Checkout");
                resetToThisRevision.Click += async (_, ev) =>
                {
                    await vm.ResetMultipleToThisRevisionAsync(changes);
                    ev.Handled = true;
                };

                var resetToFirstParent = new MenuItem();
                resetToFirstParent.Header = App.Text("ChangeCM.CheckoutFirstParentRevision");
                resetToFirstParent.Icon = this.CreateMenuIcon("Icons.File.Checkout");
                resetToFirstParent.IsEnabled = commit.Parents.Count > 0;
                resetToFirstParent.Click += async (_, ev) =>
                {
                    await vm.ResetMultipleToParentRevisionAsync(changes);
                    ev.Handled = true;
                };

                menu.Items.Add(resetToThisRevision);
                menu.Items.Add(resetToFirstParent);
                menu.Items.Add(new MenuItem { Header = "-" });
            }

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Icon = this.CreateMenuIcon("Icons.Copy");
            copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyPath.Click += async (_, ev) =>
            {
                var builder = new StringBuilder();
                foreach (var c in changes)
                    builder.AppendLine(c.Path);

                await this.CopyTextAsync(builder.ToString());
                ev.Handled = true;
            };

            var copyFullPath = new MenuItem();
            copyFullPath.Header = App.Text("CopyFullPath");
            copyFullPath.Icon = this.CreateMenuIcon("Icons.Copy");
            copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
            copyFullPath.Click += async (_, e) =>
            {
                var builder = new StringBuilder();
                foreach (var c in changes)
                    builder.AppendLine(Native.OS.GetAbsPath(repo.FullPath, c.Path));

                await this.CopyTextAsync(builder.ToString());
                e.Handled = true;
            };

            menu.Items.Add(copyPath);
            menu.Items.Add(copyFullPath);
            return menu;
        }
    }
}
