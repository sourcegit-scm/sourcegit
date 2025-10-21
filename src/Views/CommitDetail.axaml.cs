using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class CommitDetail : UserControl
    {
        public CommitDetail()
        {
            InitializeComponent();
        }

        public ContextMenu CreateChangeContextMenuByFolder(ViewModels.ChangeTreeNode node, List<Models.Change> changes)
        {
            if (DataContext is not ViewModels.CommitDetail { Repository: { } repo, Commit: { } commit } vm)
                return null;

            var fullPath = Native.OS.GetAbsPath(repo.FullPath, node.FullPath);
            var explore = new MenuItem();
            explore.Header = App.Text("RevealFile");
            explore.Icon = App.CreateMenuIcon("Icons.Explore");
            explore.IsEnabled = Directory.Exists(fullPath);
            explore.Click += (_, ev) =>
            {
                Native.OS.OpenInFileManager(fullPath, true);
                ev.Handled = true;
            };

            var history = new MenuItem();
            history.Header = App.Text("DirHistories");
            history.Icon = App.CreateMenuIcon("Icons.Histories");
            history.Click += (_, ev) =>
            {
                App.ShowWindow(new ViewModels.DirHistories(repo, node.FullPath, commit.SHA));
                ev.Handled = true;
            };

            var patch = new MenuItem();
            patch.Header = App.Text("FileCM.SaveAsPatch");
            patch.Icon = App.CreateMenuIcon("Icons.Diff");
            patch.Click += async (_, e) =>
            {
                var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
                if (storageProvider == null)
                    return;

                var options = new FilePickerSaveOptions();
                options.Title = App.Text("FileCM.SaveAsPatch");
                options.DefaultExtension = ".patch";
                options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                var storageFile = await storageProvider.SaveFilePickerAsync(options);
                if (storageFile != null)
                {
                    var saveTo = storageFile.Path.LocalPath;
                    await vm.SaveChangesAsPatchAsync(changes, saveTo);
                }

                e.Handled = true;
            };

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyPath.Click += async (_, ev) =>
            {
                await App.CopyTextAsync(node.FullPath);
                ev.Handled = true;
            };

            var copyFullPath = new MenuItem();
            copyFullPath.Header = App.Text("CopyFullPath");
            copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
            copyFullPath.Click += async (_, e) =>
            {
                await App.CopyTextAsync(fullPath);
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

        public ContextMenu CreateMultipleChangesContextMenu(List<Models.Change> changes)
        {
            if (DataContext is not ViewModels.CommitDetail { Repository: { } repo, Commit: { } commit } vm)
                return null;

            var patch = new MenuItem();
            patch.Header = App.Text("FileCM.SaveAsPatch");
            patch.Icon = App.CreateMenuIcon("Icons.Diff");
            patch.Click += async (_, e) =>
            {
                var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
                if (storageProvider == null)
                    return;

                var options = new FilePickerSaveOptions();
                options.Title = App.Text("FileCM.SaveAsPatch");
                options.DefaultExtension = ".patch";
                options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                var storageFile = await storageProvider.SaveFilePickerAsync(options);
                if (storageFile != null)
                {
                    var saveTo = storageFile.Path.LocalPath;
                    await vm.SaveChangesAsPatchAsync(changes, saveTo);
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
                resetToThisRevision.Icon = App.CreateMenuIcon("Icons.File.Checkout");
                resetToThisRevision.Click += async (_, ev) =>
                {
                    await vm.ResetMultipleToThisRevisionAsync(changes);
                    ev.Handled = true;
                };

                var resetToFirstParent = new MenuItem();
                resetToFirstParent.Header = App.Text("ChangeCM.CheckoutFirstParentRevision");
                resetToFirstParent.Icon = App.CreateMenuIcon("Icons.File.Checkout");
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
            copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyPath.Click += async (_, ev) =>
            {
                var builder = new StringBuilder();
                foreach (var c in changes)
                    builder.AppendLine(c.Path);

                await App.CopyTextAsync(builder.ToString());
                ev.Handled = true;
            };

            var copyFullPath = new MenuItem();
            copyFullPath.Header = App.Text("CopyFullPath");
            copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
            copyFullPath.Click += async (_, e) =>
            {
                var builder = new StringBuilder();
                foreach (var c in changes)
                    builder.AppendLine(Native.OS.GetAbsPath(repo.FullPath, c.Path));

                await App.CopyTextAsync(builder.ToString());
                e.Handled = true;
            };

            menu.Items.Add(copyPath);
            menu.Items.Add(copyFullPath);
            return menu;
        }

        public ContextMenu CreateChangeContextMenu(Models.Change change)
        {
            if (DataContext is not ViewModels.CommitDetail { Repository: { } repo, Commit: { } commit } vm)
                return null;

            var openWithMerger = new MenuItem();
            openWithMerger.Header = App.Text("OpenInExternalMergeTool");
            openWithMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
            openWithMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
            openWithMerger.Click += (_, ev) =>
            {
                vm.OpenChangeInMergeTool(change);
                ev.Handled = true;
            };

            var openWith = new MenuItem();
            openWith.Header = App.Text("OpenWith");
            openWith.Icon = App.CreateMenuIcon("Icons.OpenWith");
            openWith.IsEnabled = change.Index != Models.ChangeState.Deleted;
            openWith.Click += async (_, ev) =>
            {
                await vm.OpenRevisionFileWithDefaultEditorAsync(change.Path);
                ev.Handled = true;
            };

            var fullPath = Native.OS.GetAbsPath(repo.FullPath, change.Path);
            var explore = new MenuItem();
            explore.Header = App.Text("RevealFile");
            explore.Icon = App.CreateMenuIcon("Icons.Explore");
            explore.IsEnabled = File.Exists(fullPath);
            explore.Click += (_, ev) =>
            {
                Native.OS.OpenInFileManager(fullPath, true);
                ev.Handled = true;
            };

            var history = new MenuItem();
            history.Header = App.Text("FileHistory");
            history.Icon = App.CreateMenuIcon("Icons.Histories");
            history.Click += (_, ev) =>
            {
                App.ShowWindow(new ViewModels.FileHistories(repo.FullPath, change.Path, commit.SHA));
                ev.Handled = true;
            };

            var blame = new MenuItem();
            blame.Header = App.Text("Blame");
            blame.Icon = App.CreateMenuIcon("Icons.Blame");
            blame.IsEnabled = change.Index != Models.ChangeState.Deleted;
            blame.Click += (_, ev) =>
            {
                App.ShowWindow(new ViewModels.Blame(repo.FullPath, change.Path, commit));
                ev.Handled = true;
            };

            var patch = new MenuItem();
            patch.Header = App.Text("FileCM.SaveAsPatch");
            patch.Icon = App.CreateMenuIcon("Icons.Diff");
            patch.Click += async (_, e) =>
            {
                var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
                if (storageProvider == null)
                    return;

                var options = new FilePickerSaveOptions();
                options.Title = App.Text("FileCM.SaveAsPatch");
                options.DefaultExtension = ".patch";
                options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                var storageFile = await storageProvider.SaveFilePickerAsync(options);
                if (storageFile != null)
                {
                    var saveTo = storageFile.Path.LocalPath;
                    await vm.SaveChangesAsPatchAsync([change], saveTo);
                }

                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(openWithMerger);
            menu.Items.Add(openWith);
            menu.Items.Add(explore);
            menu.Items.Add(new MenuItem { Header = "-" });
            menu.Items.Add(history);
            menu.Items.Add(blame);
            menu.Items.Add(patch);
            menu.Items.Add(new MenuItem { Header = "-" });

            if (!repo.IsBare)
            {
                var resetToThisRevision = new MenuItem();
                resetToThisRevision.Header = App.Text("ChangeCM.CheckoutThisRevision");
                resetToThisRevision.Icon = App.CreateMenuIcon("Icons.File.Checkout");
                resetToThisRevision.Click += async (_, ev) =>
                {
                    await vm.ResetToThisRevisionAsync(change.Path);
                    ev.Handled = true;
                };

                var resetToFirstParent = new MenuItem();
                resetToFirstParent.Header = App.Text("ChangeCM.CheckoutFirstParentRevision");
                resetToFirstParent.Icon = App.CreateMenuIcon("Icons.File.Checkout");
                resetToFirstParent.IsEnabled = commit.Parents.Count > 0;
                resetToFirstParent.Click += async (_, ev) =>
                {
                    await vm.ResetToParentRevisionAsync(change);
                    ev.Handled = true;
                };

                menu.Items.Add(resetToThisRevision);
                menu.Items.Add(resetToFirstParent);
                menu.Items.Add(new MenuItem { Header = "-" });

                if (repo.Remotes.Count > 0 && File.Exists(fullPath) && repo.IsLFSEnabled())
                {
                    var lfs = new MenuItem();
                    lfs.Header = App.Text("GitLFS");
                    lfs.Icon = App.CreateMenuIcon("Icons.LFS");

                    var lfsLock = new MenuItem();
                    lfsLock.Header = App.Text("GitLFS.Locks.Lock");
                    lfsLock.Icon = App.CreateMenuIcon("Icons.Lock");
                    if (repo.Remotes.Count == 1)
                    {
                        lfsLock.Click += async (_, e) =>
                        {
                            await repo.LockLFSFileAsync(repo.Remotes[0].Name, change.Path);
                            e.Handled = true;
                        };
                    }
                    else
                    {
                        foreach (var remote in repo.Remotes)
                        {
                            var remoteName = remote.Name;
                            var lockRemote = new MenuItem();
                            lockRemote.Header = remoteName;
                            lockRemote.Click += async (_, e) =>
                            {
                                await repo.LockLFSFileAsync(remoteName, change.Path);
                                e.Handled = true;
                            };
                            lfsLock.Items.Add(lockRemote);
                        }
                    }
                    lfs.Items.Add(lfsLock);

                    var lfsUnlock = new MenuItem();
                    lfsUnlock.Header = App.Text("GitLFS.Locks.Unlock");
                    lfsUnlock.Icon = App.CreateMenuIcon("Icons.Unlock");
                    if (repo.Remotes.Count == 1)
                    {
                        lfsUnlock.Click += async (_, e) =>
                        {
                            await repo.UnlockLFSFileAsync(repo.Remotes[0].Name, change.Path, false, true);
                            e.Handled = true;
                        };
                    }
                    else
                    {
                        foreach (var remote in repo.Remotes)
                        {
                            var remoteName = remote.Name;
                            var unlockRemote = new MenuItem();
                            unlockRemote.Header = remoteName;
                            unlockRemote.Click += async (_, e) =>
                            {
                                await repo.UnlockLFSFileAsync(remoteName, change.Path, false, true);
                                e.Handled = true;
                            };
                            lfsUnlock.Items.Add(unlockRemote);
                        }
                    }
                    lfs.Items.Add(lfsUnlock);

                    menu.Items.Add(lfs);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }
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
            copyFullPath.Click += async (_, e) =>
            {
                await App.CopyTextAsync(fullPath);
                e.Handled = true;
            };

            menu.Items.Add(copyPath);
            menu.Items.Add(copyFullPath);
            return menu;
        }

        private async void OnCommitListKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.CommitDetail vm)
                return;

            if (sender is not ListBox { SelectedItem: Models.Change change })
                return;

            if (e.Key == Key.C &&
                e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    await App.CopyTextAsync(vm.GetAbsPath(change.Path));
                else
                    await App.CopyTextAsync(change.Path);

                e.Handled = true;
                return;
            }

            if (e.Key == Key.D &&
                e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) &&
                e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                vm.OpenChangeInMergeTool(change);
                e.Handled = true;
            }
        }

        private void OnChangeDoubleTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail && sender is Grid { DataContext: Models.Change change })
            {
                var tabControl = this.FindLogicalDescendantOfType<TabControl>();
                if (tabControl != null)
                    tabControl.SelectedIndex = 1;

                detail.SelectedChanges = new() { change };
            }

            e.Handled = true;
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is Grid { DataContext: Models.Change change } grid)
                CreateChangeContextMenu(change)?.Open(grid);
            e.Handled = true;
        }
    }
}
