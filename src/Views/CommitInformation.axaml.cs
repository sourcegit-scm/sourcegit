using System;
using System.Collections.Generic;
using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    // The entire Information tab body is shared by single commits and multi-selection cards.
    public partial class CommitInformation : UserControl
    {
        public static readonly StyledProperty<bool> OpenChangesInNewWindowProperty =
            AvaloniaProperty.Register<CommitInformation, bool>(nameof(OpenChangesInNewWindow));

        public bool OpenChangesInNewWindow
        {
            get => GetValue(OpenChangesInNewWindowProperty);
            set => SetValue(OpenChangesInNewWindowProperty, value);
        }

        public CommitInformation()
        {
            InitializeComponent();
            AddHandler(PointerPressedEvent, OnCardPointerPressed, RoutingStrategies.Tunnel);
        }

        private void OnCardPointerPressed(object sender, PointerPressedEventArgs e)
        {
            // Inner links, buttons and the file list may handle the bubbling event.
            // Select the card first without touching the history selection.
            if (OpenChangesInNewWindow && this.FindAncestorOfType<ListBoxItem>() is { } card)
                card.IsSelected = true;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            EnsureInformationLoaded();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (this.GetVisualRoot() != null)
                EnsureInformationLoaded();
        }

        private void EnsureInformationLoaded()
        {
            if (DataContext is ViewModels.CommitDetail detail)
                detail.EnsureInformationLoaded();
        }

        public static ContextMenu CreateChangeContextMenu(Control owner, Models.Change change)
        {
            if (owner.DataContext is not ViewModels.CommitDetail { Repository: { } repo, Commit: { } commit } vm)
                return null;

            var openWith = new MenuItem();
            openWith.Header = App.Text("Open");
            openWith.Icon = owner.CreateMenuIcon("Icons.OpenWith");
            openWith.IsEnabled = change.Index != Models.ChangeState.Deleted;
            if (openWith.IsEnabled)
            {
                var defaultEditor = new MenuItem();
                defaultEditor.Header = App.Text("Open.SystemDefaultEditor");
                defaultEditor.Click += async (_, ev) =>
                {
                    await vm.OpenRevisionFileAsync(change.Path, null);
                    ev.Handled = true;
                };

                openWith.Items.Add(defaultEditor);

                var tools = Native.OS.ExternalTools;
                if (tools.Count > 0)
                {
                    openWith.Items.Add(new MenuItem() { Header = "-" });

                    for (var i = 0; i < tools.Count; i++)
                    {
                        var tool = tools[i];
                        var item = new MenuItem();
                        item.Header = tool.Name;
                        item.Icon = new Image { Width = 16, Height = 16, Source = tool.IconImage };
                        item.Click += async (_, ev) =>
                        {
                            await vm.OpenRevisionFileAsync(change.Path, tool);
                            ev.Handled = true;
                        };

                        openWith.Items.Add(item);
                    }
                }
            }

            var openWithMerger = new MenuItem();
            openWithMerger.Header = App.Text("OpenInExternalMergeTool");
            openWithMerger.Icon = owner.CreateMenuIcon("Icons.OpenWith");
            openWithMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
            openWithMerger.Click += (_, ev) =>
            {
                vm.OpenChangeInMergeTool(change);
                ev.Handled = true;
            };

            var fullPath = Native.OS.GetAbsPath(repo.FullPath, change.Path);
            var explore = new MenuItem();
            explore.Header = App.Text("RevealFile");
            explore.Icon = owner.CreateMenuIcon("Icons.Explore");
            explore.IsEnabled = File.Exists(fullPath);
            explore.Click += (_, ev) =>
            {
                Native.OS.OpenInFileManager(fullPath);
                ev.Handled = true;
            };

            var history = new MenuItem();
            history.Header = App.Text("FileHistory");
            history.Icon = owner.CreateMenuIcon("Icons.Histories");
            history.Click += (_, ev) =>
            {
                owner.ShowWindow(new ViewModels.FileHistories(repo.FullPath, change.Path, commit.SHA));
                ev.Handled = true;
            };

            var blame = new MenuItem();
            blame.Header = App.Text("Blame");
            blame.Icon = owner.CreateMenuIcon("Icons.Blame");
            blame.IsEnabled = change.Index != Models.ChangeState.Deleted;
            blame.Click += (_, ev) =>
            {
                owner.ShowWindow(new ViewModels.Blame(repo.FullPath, change.Path, commit));
                ev.Handled = true;
            };

            var patch = new MenuItem();
            patch.Header = App.Text("FileCM.SaveAsPatch");
            patch.Icon = owner.CreateMenuIcon("Icons.Save");
            patch.Click += async (_, e) =>
            {
                var storageProvider = TopLevel.GetTopLevel(owner)?.StorageProvider;
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
                        await vm.SaveChangesAsPatchAsync([change], saveTo);
                    }
                }
                catch (Exception exception)
                {
                    repo.SendNotification($"Failed to save as patch: {exception.Message}", true);
                }

                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(openWith);
            menu.Items.Add(openWithMerger);
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
                resetToThisRevision.Icon = owner.CreateMenuIcon("Icons.File.Checkout");
                resetToThisRevision.Click += async (_, ev) =>
                {
                    await vm.ResetToThisRevisionAsync(change);
                    ev.Handled = true;
                };

                var resetToFirstParent = new MenuItem();
                resetToFirstParent.Header = App.Text("ChangeCM.CheckoutFirstParentRevision");
                resetToFirstParent.Icon = owner.CreateMenuIcon("Icons.File.Checkout");
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
                    lfs.Icon = owner.CreateMenuIcon("Icons.LFS");

                    var lfsLock = new MenuItem();
                    lfsLock.Header = App.Text("GitLFS.Locks.Lock");
                    lfsLock.Icon = owner.CreateMenuIcon("Icons.Lock");
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
                    lfsUnlock.Icon = owner.CreateMenuIcon("Icons.Unlock");
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

            var actions = repo.GetCustomActions(Models.CustomActionScope.File);
            if (actions.Count > 0)
            {
                var target = new Models.CustomActionTargetFile(change.Path, vm.Commit);
                var custom = new MenuItem();
                custom.Header = App.Text("FileCM.CustomAction");
                custom.Icon = owner.CreateMenuIcon("Icons.Action");

                foreach (var action in actions)
                {
                    var (dup, label) = action;
                    var item = new MenuItem();
                    item.Icon = owner.CreateMenuIcon("Icons.Action");
                    item.Header = label;
                    item.Click += async (_, e) =>
                    {
                        await repo.ExecCustomActionAsync(dup, target);
                        e.Handled = true;
                    };

                    custom.Items.Add(item);
                }

                menu.Items.Add(custom);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Icon = owner.CreateMenuIcon("Icons.Copy");
            copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyPath.Click += async (_, ev) =>
            {
                await owner.CopyTextAsync(change.Path);
                ev.Handled = true;
            };

            var copyFullPath = new MenuItem();
            copyFullPath.Header = App.Text("CopyFullPath");
            copyFullPath.Icon = owner.CreateMenuIcon("Icons.Copy");
            copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
            copyFullPath.Click += async (_, e) =>
            {
                await owner.CopyTextAsync(fullPath);
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
                    await this.CopyTextAsync(vm.GetAbsPath(change.Path));
                else
                    await this.CopyTextAsync(change.Path);

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
                if (OpenChangesInNewWindow)
                {
                    OpenStandaloneChanges(detail, change);
                }
                else
                {
                    detail.ChangeSelection = new(new List<Models.Change> { change });
                    detail.ActiveTabIndex = 1;
                }
            }

            e.Handled = true;
        }

        private void OnOpenAllChanges(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail)
                OpenStandaloneChanges(detail, null);
            e.Handled = true;
        }

        private void OpenStandaloneChanges(ViewModels.CommitDetail detail, Models.Change change)
        {
            var standalone = new CommitDetailStandalone();
            var cloned = detail.Clone(change);
            if (change != null)
                cloned.ChangeSelection = new(new List<Models.Change> { change });
            cloned.ActiveTabIndex = 1;
            standalone.DataContext = cloned;
            standalone.Closed += (_, _) => cloned.Dispose();
            this.ShowWindow(standalone);
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is Grid { DataContext: Models.Change change } grid)
                CreateChangeContextMenu(this, change)?.Open(grid);
            e.Handled = true;
        }
    }
}
