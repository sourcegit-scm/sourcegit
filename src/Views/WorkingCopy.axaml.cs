using System;
using System.IO;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class WorkingCopy : UserControl
    {
        public WorkingCopy()
        {
            InitializeComponent();
        }

        private void OnMainLayoutSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is not Grid grid)
                return;

            var layout = ViewModels.Preferences.Instance.Layout;
            var width = grid.Bounds.Width;
            var maxLeft = width - 304;

            if (layout.WorkingCopyLeftWidth.Value - maxLeft > 1.0)
                layout.WorkingCopyLeftWidth = new GridLength(maxLeft, GridUnitType.Pixel);
        }

        private async void OnOpenAssumeUnchanged(object sender, RoutedEventArgs e)
        {
            var repoView = this.FindAncestorOfType<Repository>();
            if (repoView is { DataContext: ViewModels.Repository repo })
                await App.ShowDialog(new ViewModels.AssumeUnchangedManager(repo));

            e.Handled = true;
        }

        private void OnUnstagedContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm && sender is Control control)
            {
                var container = control.FindDescendantOfType<ChangeCollectionContainer>();
                var selectedSingleFolder = string.Empty;
                if (container is { SelectedItems.Count: 1, SelectedItem: ViewModels.ChangeTreeNode { IsFolder: true } node })
                    selectedSingleFolder = node.FullPath;

                var menu = CreateContextMenuForUnstagedChanges(vm, selectedSingleFolder);
                menu?.Open(control);
                e.Handled = true;
            }
        }

        private void OnStagedContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm && sender is Control control)
            {
                var container = control.FindDescendantOfType<ChangeCollectionContainer>();
                var selectedSingleFolder = string.Empty;
                if (container is { SelectedItems.Count: 1, SelectedItem: ViewModels.ChangeTreeNode { IsFolder: true } node })
                    selectedSingleFolder = node.FullPath;

                var menu = CreateContextMenuForStagedChanges(vm, selectedSingleFolder);
                menu?.Open(control);
                e.Handled = true;
            }
        }

        private async void OnUnstagedChangeDoubleTapped(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                await vm.StageChangesAsync(vm.SelectedUnstaged, next);
                UnstagedChangesView.TakeFocus();
                e.Handled = true;
            }
        }

        private async void OnStagedChangeDoubleTapped(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                var next = StagedChangesView.GetNextChangeWithoutSelection();
                await vm.UnstageChangesAsync(vm.SelectedStaged, next);
                StagedChangesView.TakeFocus();
                e.Handled = true;
            }
        }

        private async void OnUnstagedKeyDown(object _, KeyEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                if (e.Key is Key.Space or Key.Enter)
                {
                    var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                    await vm.StageChangesAsync(vm.SelectedUnstaged, next);
                    UnstagedChangesView.TakeFocus();
                    e.Handled = true;
                }
                else if (e.Key is Key.Delete or Key.Back && vm.SelectedUnstaged is { Count: > 0 })
                {
                    vm.Discard(vm.SelectedUnstaged);
                    e.Handled = true;
                }
                else if (e.Key is Key.O &&
                    e.KeyModifiers == (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) &&
                    vm.SelectedUnstaged is { Count: 1 })
                {
                    var change = vm.SelectedUnstaged[0];
                    var fullpath = Native.OS.GetAbsPath(vm.Repository.FullPath, change.Path);
                    if (File.Exists(fullpath))
                        Native.OS.OpenWithDefaultEditor(fullpath);
                    e.Handled = true;
                }
                else if (e.Key is Key.C &&
                         e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) &&
                         vm.SelectedUnstaged is { Count: 1 })
                {
                    var change = vm.SelectedUnstaged[0];
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                        await App.CopyTextAsync(Native.OS.GetAbsPath(vm.Repository.FullPath, change.Path));
                    else
                        await App.CopyTextAsync(change.Path);

                    e.Handled = true;
                }
            }
        }

        private async void OnStagedKeyDown(object _, KeyEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                if (e.Key is Key.Space or Key.Enter)
                {
                    var next = StagedChangesView.GetNextChangeWithoutSelection();
                    await vm.UnstageChangesAsync(vm.SelectedStaged, next);
                    StagedChangesView.TakeFocus();
                    e.Handled = true;
                }
                else if (e.Key is Key.O &&
                    e.KeyModifiers == (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) &&
                    vm.SelectedStaged is { Count: 1 })
                {
                    var change = vm.SelectedStaged[0];
                    var fullpath = Native.OS.GetAbsPath(vm.Repository.FullPath, change.Path);
                    if (File.Exists(fullpath))
                        Native.OS.OpenWithDefaultEditor(fullpath);
                    e.Handled = true;
                }
                else if (e.Key is Key.C &&
                         e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) &&
                         vm.SelectedStaged is { Count: 1 })
                {
                    var change = vm.SelectedStaged[0];
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                        await App.CopyTextAsync(Native.OS.GetAbsPath(vm.Repository.FullPath, change.Path));
                    else
                        await App.CopyTextAsync(change.Path);

                    e.Handled = true;
                }
            }
        }

        private async void OnStageSelectedButtonClicked(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                await vm.StageChangesAsync(vm.SelectedUnstaged, next);
                UnstagedChangesView.TakeFocus();
            }

            e.Handled = true;
        }

        private async void OnStageAllButtonClicked(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.StageChangesAsync(vm.VisibleUnstaged, null);

            e.Handled = true;
        }

        private async void OnUnstageSelectedButtonClicked(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                var next = StagedChangesView.GetNextChangeWithoutSelection();
                await vm.UnstageChangesAsync(vm.SelectedStaged, next);
                StagedChangesView.TakeFocus();
            }

            e.Handled = true;
        }

        private async void OnUnstageAllButtonClicked(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.UnstageChangesAsync(vm.VisibleStaged, null);

            e.Handled = true;
        }

        private async void OnOpenExternalMergeToolAllConflicts(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.UseExternalMergeToolAsync(null);

            e.Handled = true;
        }

        private async void OnContinue(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.ContinueMergeAsync();

            e.Handled = true;
        }

        private async void OnCommit(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.CommitAsync(false, false);

            e.Handled = true;
        }

        private async void OnCommitWithAutoStage(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.CommitAsync(true, false);

            e.Handled = true;
        }

        private async void OnCommitWithPush(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.CommitAsync(false, true);

            e.Handled = true;
        }

        private ContextMenu CreateContextMenuForUnstagedChanges(ViewModels.WorkingCopy vm, string selectedSingleFolder)
        {
            var repo = vm.Repository;
            var selectedUnstaged = vm.SelectedUnstaged;
            if (repo == null || selectedUnstaged == null || selectedUnstaged.Count == 0)
                return null;

            var hasSelectedFolder = !string.IsNullOrEmpty(selectedSingleFolder);
            var menu = new ContextMenu();
            if (selectedUnstaged.Count == 1)
            {
                var change = selectedUnstaged[0];
                var path = Native.OS.GetAbsPath(repo.FullPath, change.Path);
                TryAddOpenFileToContextMenu(menu, path);

                if (!change.IsConflicted || change.ConflictReason is Models.ConflictReason.BothAdded or Models.ConflictReason.BothModified)
                {
                    var openMerger = new MenuItem();
                    openMerger.Header = App.Text("OpenInExternalMergeTool");
                    openMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
                    openMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
                    openMerger.Click += async (_, e) =>
                    {
                        if (change.IsConflicted)
                            await vm.UseExternalMergeToolAsync(change);
                        else
                            vm.UseExternalDiffTool(change, true);

                        e.Handled = true;
                    };
                    menu.Items.Add(openMerger);
                }

                var explore = new MenuItem();
                explore.Header = App.Text("RevealFile");
                explore.Icon = App.CreateMenuIcon("Icons.Explore");
                explore.IsEnabled = File.Exists(path) || Directory.Exists(path);
                explore.Click += (_, e) =>
                {
                    var target = hasSelectedFolder ? Native.OS.GetAbsPath(repo.FullPath, selectedSingleFolder) : path;
                    Native.OS.OpenInFileManager(target, true);
                    e.Handled = true;
                };
                menu.Items.Add(explore);
                menu.Items.Add(new MenuItem() { Header = "-" });

                if (change.IsConflicted)
                {
                    var useTheirs = new MenuItem();
                    useTheirs.Icon = App.CreateMenuIcon("Icons.Incoming");
                    useTheirs.Click += async (_, e) =>
                    {
                        await vm.UseTheirsAsync(selectedUnstaged);
                        e.Handled = true;
                    };

                    var useMine = new MenuItem();
                    useMine.Icon = App.CreateMenuIcon("Icons.Local");
                    useMine.Click += async (_, e) =>
                    {
                        await vm.UseMineAsync(selectedUnstaged);
                        e.Handled = true;
                    };

                    switch (vm.InProgressContext)
                    {
                        case ViewModels.CherryPickInProgress cherryPick:
                            useTheirs.Header = App.Text("FileCM.ResolveUsing", cherryPick.HeadName);
                            useMine.Header = App.Text("FileCM.ResolveUsing", repo.CurrentBranch.Name);
                            break;
                        case ViewModels.RebaseInProgress rebase:
                            useTheirs.Header = App.Text("FileCM.ResolveUsing", rebase.HeadName);
                            useMine.Header = App.Text("FileCM.ResolveUsing", rebase.BaseName);
                            break;
                        case ViewModels.RevertInProgress revert:
                            useTheirs.Header = App.Text("FileCM.ResolveUsing", $"{revert.Head.SHA.AsSpan(0, 10)} (revert)");
                            useMine.Header = App.Text("FileCM.ResolveUsing", repo.CurrentBranch.Name);
                            break;
                        case ViewModels.MergeInProgress merge:
                            useTheirs.Header = App.Text("FileCM.ResolveUsing", merge.SourceName);
                            useMine.Header = App.Text("FileCM.ResolveUsing", repo.CurrentBranch.Name);
                            break;
                        default:
                            useTheirs.Header = App.Text("FileCM.UseTheirs");
                            useMine.Header = App.Text("FileCM.UseMine");
                            break;
                    }

                    menu.Items.Add(useTheirs);
                    menu.Items.Add(useMine);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }
                else
                {
                    var stage = new MenuItem();
                    stage.Header = App.Text("FileCM.Stage");
                    stage.Icon = App.CreateMenuIcon("Icons.File.Add");
                    stage.Tag = "Enter/Space";
                    stage.Click += async (_, e) =>
                    {
                        await vm.StageChangesAsync(selectedUnstaged, null);
                        e.Handled = true;
                    };

                    var discard = new MenuItem();
                    discard.Header = App.Text("FileCM.Discard");
                    discard.Icon = App.CreateMenuIcon("Icons.Undo");
                    discard.Tag = "Back/Delete";
                    discard.Click += (_, e) =>
                    {
                        vm.Discard(selectedUnstaged);
                        e.Handled = true;
                    };

                    var stash = new MenuItem();
                    stash.Header = App.Text("FileCM.Stash");
                    stash.Icon = App.CreateMenuIcon("Icons.Stashes.Add");
                    stash.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.StashChanges(repo, selectedUnstaged));

                        e.Handled = true;
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

                        try
                        {
                            var storageFile = await storageProvider.SaveFilePickerAsync(options);
                            if (storageFile != null)
                                await vm.SaveChangesToPatchAsync(selectedUnstaged, true, storageFile.Path.LocalPath);
                        }
                        catch (Exception exception)
                        {
                            App.RaiseException(repo.FullPath, $"Failed to save as patch: {exception.Message}");
                        }

                        e.Handled = true;
                    };

                    var assumeUnchanged = new MenuItem();
                    assumeUnchanged.Header = App.Text("FileCM.AssumeUnchanged");
                    assumeUnchanged.Icon = App.CreateMenuIcon("Icons.File.Ignore");
                    assumeUnchanged.IsVisible = change.WorkTree != Models.ChangeState.Untracked;
                    assumeUnchanged.Click += async (_, e) =>
                    {
                        var log = repo.CreateLog("Assume File Unchanged");
                        await new Commands.AssumeUnchanged(repo.FullPath, change.Path, true).Use(log).ExecAsync();
                        log.Complete();
                        e.Handled = true;
                    };

                    menu.Items.Add(stage);
                    menu.Items.Add(discard);
                    menu.Items.Add(stash);
                    menu.Items.Add(patch);
                    menu.Items.Add(assumeUnchanged);
                    menu.Items.Add(new MenuItem() { Header = "-" });

                    var extension = Path.GetExtension(change.Path);
                    var hasExtra = false;
                    if (change.WorkTree == Models.ChangeState.Untracked)
                    {
                        var addToIgnore = new MenuItem();
                        addToIgnore.Header = App.Text("WorkingCopy.AddToGitIgnore");
                        addToIgnore.Icon = App.CreateMenuIcon("Icons.GitIgnore");

                        if (hasSelectedFolder)
                        {
                            var ignoreFolder = new MenuItem();
                            ignoreFolder.Header = App.Text("WorkingCopy.AddToGitIgnore.InFolder");
                            ignoreFolder.Click += (_, e) =>
                            {
                                if (repo.CanCreatePopup())
                                    repo.ShowPopup(new ViewModels.AddToIgnore(repo, $"{selectedSingleFolder}/"));
                                e.Handled = true;
                            };
                            addToIgnore.Items.Add(ignoreFolder);
                        }
                        else
                        {
                            var isRooted = change.Path!.IndexOf('/') <= 0;
                            var singleFile = new MenuItem();
                            singleFile.Header = App.Text("WorkingCopy.AddToGitIgnore.SingleFile");
                            singleFile.Click += (_, e) =>
                            {
                                if (repo.CanCreatePopup())
                                    repo.ShowPopup(new ViewModels.AddToIgnore(repo, change.Path));
                                e.Handled = true;
                            };
                            addToIgnore.Items.Add(singleFile);

                            if (!string.IsNullOrEmpty(extension))
                            {
                                var byExtension = new MenuItem();
                                byExtension.Header = App.Text("WorkingCopy.AddToGitIgnore.Extension", extension);
                                byExtension.Click += (_, e) =>
                                {
                                    if (repo.CanCreatePopup())
                                        repo.ShowPopup(new ViewModels.AddToIgnore(repo, $"*{extension}"));
                                    e.Handled = true;
                                };
                                addToIgnore.Items.Add(byExtension);

                                var byExtensionInSameFolder = new MenuItem();
                                byExtensionInSameFolder.Header = App.Text("WorkingCopy.AddToGitIgnore.ExtensionInSameFolder", extension);
                                byExtensionInSameFolder.IsVisible = !isRooted;
                                byExtensionInSameFolder.Click += (_, e) =>
                                {
                                    var dir = Path.GetDirectoryName(change.Path)!.Replace('\\', '/').TrimEnd('/');
                                    if (repo.CanCreatePopup())
                                        repo.ShowPopup(new ViewModels.AddToIgnore(repo, $"{dir}/*{extension}"));
                                    e.Handled = true;
                                };
                                addToIgnore.Items.Add(byExtensionInSameFolder);
                            }
                        }

                        menu.Items.Add(addToIgnore);
                        hasExtra = true;
                    }
                    else if (hasSelectedFolder)
                    {
                        var addToIgnore = new MenuItem();
                        addToIgnore.Header = App.Text("WorkingCopy.AddToGitIgnore");
                        addToIgnore.Icon = App.CreateMenuIcon("Icons.GitIgnore");

                        var ignoreFolder = new MenuItem();
                        ignoreFolder.Header = App.Text("WorkingCopy.AddToGitIgnore.InFolder");
                        ignoreFolder.Click += (_, e) =>
                        {
                            if (repo.CanCreatePopup())
                                repo.ShowPopup(new ViewModels.AddToIgnore(repo, $"{selectedSingleFolder}/"));
                            e.Handled = true;
                        };
                        addToIgnore.Items.Add(ignoreFolder);

                        menu.Items.Add(addToIgnore);
                        hasExtra = true;
                    }

                    if (repo.IsLFSEnabled())
                    {
                        var lfs = new MenuItem();
                        lfs.Header = App.Text("GitLFS");
                        lfs.Icon = App.CreateMenuIcon("Icons.LFS");

                        var isLFSFiltered = new Commands.IsLFSFiltered(repo.FullPath, change.Path).GetResult();
                        if (!isLFSFiltered)
                        {
                            var filename = Path.GetFileName(change.Path);
                            var lfsTrackThisFile = new MenuItem();
                            lfsTrackThisFile.Header = App.Text("GitLFS.Track", filename);
                            lfsTrackThisFile.Click += async (_, e) =>
                            {
                                await repo.TrackLFSFileAsync(filename, true);
                                e.Handled = true;
                            };
                            lfs.Items.Add(lfsTrackThisFile);

                            if (!string.IsNullOrEmpty(extension))
                            {
                                var lfsTrackByExtension = new MenuItem();
                                lfsTrackByExtension.Header = App.Text("GitLFS.TrackByExtension", extension);
                                lfsTrackByExtension.Click += async (_, e) =>
                                {
                                    await repo.TrackLFSFileAsync($"*{extension}", false);
                                    e.Handled = true;
                                };
                                lfs.Items.Add(lfsTrackByExtension);
                            }

                            lfs.Items.Add(new MenuItem() { Header = "-" });
                        }

                        var lfsLock = new MenuItem();
                        lfsLock.Header = App.Text("GitLFS.Locks.Lock");
                        lfsLock.Icon = App.CreateMenuIcon("Icons.Lock");
                        lfsLock.IsEnabled = repo.Remotes.Count > 0;
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
                        lfsUnlock.IsEnabled = repo.Remotes.Count > 0;
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
                        hasExtra = true;
                    }

                    if (hasExtra)
                        menu.Items.Add(new MenuItem() { Header = "-" });
                }

                if (hasSelectedFolder)
                {
                    var history = new MenuItem();
                    history.Header = App.Text("DirHistories");
                    history.Icon = App.CreateMenuIcon("Icons.Histories");
                    history.Click += (_, e) =>
                    {
                        App.ShowWindow(new ViewModels.DirHistories(repo, selectedSingleFolder));
                        e.Handled = true;
                    };

                    menu.Items.Add(history);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }
                else if (change.WorkTree is not (Models.ChangeState.Untracked or Models.ChangeState.Added))
                {
                    var history = new MenuItem();
                    history.Header = App.Text("FileHistory");
                    history.Icon = App.CreateMenuIcon("Icons.Histories");
                    history.Click += (_, e) =>
                    {
                        App.ShowWindow(new ViewModels.FileHistories(repo.FullPath, change.Path));
                        e.Handled = true;
                    };

                    var blame = new MenuItem();
                    blame.Header = App.Text("Blame") + " (HEAD-only)";
                    blame.Icon = App.CreateMenuIcon("Icons.Blame");
                    blame.Click += async (_, ev) =>
                    {
                        var commit = await new Commands.QuerySingleCommit(repo.FullPath, "HEAD").GetResultAsync();
                        App.ShowWindow(new ViewModels.Blame(repo.FullPath, change.Path, commit));
                        ev.Handled = true;
                    };

                    menu.Items.Add(history);
                    menu.Items.Add(blame);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                TryToAddCustomActionsToContextMenu(repo, menu, change.Path);

                var copy = new MenuItem();
                copy.Header = App.Text("CopyPath");
                copy.Icon = App.CreateMenuIcon("Icons.Copy");
                copy.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                copy.Click += async (_, e) =>
                {
                    await App.CopyTextAsync(hasSelectedFolder ? selectedSingleFolder : change.Path);
                    e.Handled = true;
                };

                var copyFullPath = new MenuItem();
                copyFullPath.Header = App.Text("CopyFullPath");
                copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
                copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                copyFullPath.Click += async (_, e) =>
                {
                    await App.CopyTextAsync(hasSelectedFolder ? Native.OS.GetAbsPath(repo.FullPath, selectedSingleFolder) : path);
                    e.Handled = true;
                };

                menu.Items.Add(copy);
                menu.Items.Add(copyFullPath);
            }
            else
            {
                var hasConflicts = false;
                var hasNonConflicts = false;
                foreach (var change in selectedUnstaged)
                {
                    if (change.IsConflicted)
                        hasConflicts = true;
                    else
                        hasNonConflicts = true;
                }

                if (hasConflicts)
                {
                    if (hasNonConflicts)
                    {
                        App.RaiseException(repo.FullPath, "Selection contains both conflict and non-conflict changes!");
                        return null;
                    }

                    var useTheirs = new MenuItem();
                    useTheirs.Icon = App.CreateMenuIcon("Icons.Incoming");
                    useTheirs.Click += async (_, e) =>
                    {
                        await vm.UseTheirsAsync(selectedUnstaged);
                        e.Handled = true;
                    };

                    var useMine = new MenuItem();
                    useMine.Icon = App.CreateMenuIcon("Icons.Local");
                    useMine.Click += async (_, e) =>
                    {
                        await vm.UseMineAsync(selectedUnstaged);
                        e.Handled = true;
                    };

                    switch (vm.InProgressContext)
                    {
                        case ViewModels.CherryPickInProgress cherryPick:
                            useTheirs.Header = App.Text("FileCM.ResolveUsing", cherryPick.HeadName);
                            useMine.Header = App.Text("FileCM.ResolveUsing", repo.CurrentBranch.Name);
                            break;
                        case ViewModels.RebaseInProgress rebase:
                            useTheirs.Header = App.Text("FileCM.ResolveUsing", rebase.HeadName);
                            useMine.Header = App.Text("FileCM.ResolveUsing", rebase.BaseName);
                            break;
                        case ViewModels.RevertInProgress revert:
                            useTheirs.Header = App.Text("FileCM.ResolveUsing", $"{revert.Head.SHA.AsSpan(0, 10)} (revert)");
                            useMine.Header = App.Text("FileCM.ResolveUsing", repo.CurrentBranch.Name);
                            break;
                        case ViewModels.MergeInProgress merge:
                            useTheirs.Header = App.Text("FileCM.ResolveUsing", merge.SourceName);
                            useMine.Header = App.Text("FileCM.ResolveUsing", repo.CurrentBranch.Name);
                            break;
                        default:
                            useTheirs.Header = App.Text("FileCM.UseTheirs");
                            useMine.Header = App.Text("FileCM.UseMine");
                            break;
                    }

                    menu.Items.Add(useTheirs);
                    menu.Items.Add(useMine);
                    return menu;
                }

                if (hasSelectedFolder)
                {
                    var dir = Path.Combine(repo.FullPath, selectedSingleFolder);
                    var explore = new MenuItem();
                    explore.Header = App.Text("RevealFile");
                    explore.Icon = App.CreateMenuIcon("Icons.Explore");
                    explore.IsEnabled = Directory.Exists(dir);
                    explore.Click += (_, e) =>
                    {
                        Native.OS.OpenInFileManager(dir, true);
                        e.Handled = true;
                    };
                    menu.Items.Add(explore);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                var stage = new MenuItem();
                stage.Header = App.Text("FileCM.StageMulti", selectedUnstaged.Count);
                stage.Icon = App.CreateMenuIcon("Icons.File.Add");
                stage.Tag = "Enter/Space";
                stage.Click += async (_, e) =>
                {
                    await vm.StageChangesAsync(selectedUnstaged, null);
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Text("FileCM.DiscardMulti", selectedUnstaged.Count);
                discard.Icon = App.CreateMenuIcon("Icons.Undo");
                discard.Tag = "Back/Delete";
                discard.Click += (_, e) =>
                {
                    vm.Discard(selectedUnstaged);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.StashMulti", selectedUnstaged.Count);
                stash.Icon = App.CreateMenuIcon("Icons.Stashes.Add");
                stash.Click += (_, e) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.StashChanges(repo, selectedUnstaged));

                    e.Handled = true;
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

                    try
                    {
                        var storageFile = await storageProvider.SaveFilePickerAsync(options);
                        if (storageFile != null)
                            await vm.SaveChangesToPatchAsync(selectedUnstaged, true, storageFile.Path.LocalPath);
                    }
                    catch (Exception exception)
                    {
                        App.RaiseException(repo.FullPath, $"Failed to save as patch: {exception.Message}");
                    }

                    e.Handled = true;
                };

                menu.Items.Add(stage);
                menu.Items.Add(discard);
                menu.Items.Add(stash);
                menu.Items.Add(patch);

                if (hasSelectedFolder)
                {
                    var ignoreFolder = new MenuItem();
                    ignoreFolder.Header = App.Text("WorkingCopy.AddToGitIgnore.InFolder");
                    ignoreFolder.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.AddToIgnore(repo, $"{selectedSingleFolder}/"));
                        e.Handled = true;
                    };

                    var addToIgnore = new MenuItem();
                    addToIgnore.Header = App.Text("WorkingCopy.AddToGitIgnore");
                    addToIgnore.Icon = App.CreateMenuIcon("Icons.GitIgnore");
                    addToIgnore.Items.Add(ignoreFolder);

                    var history = new MenuItem();
                    history.Header = App.Text("DirHistories");
                    history.Icon = App.CreateMenuIcon("Icons.Histories");
                    history.Click += (_, e) =>
                    {
                        App.ShowWindow(new ViewModels.DirHistories(repo, selectedSingleFolder));
                        e.Handled = true;
                    };

                    var copy = new MenuItem();
                    copy.Header = App.Text("CopyPath");
                    copy.Icon = App.CreateMenuIcon("Icons.Copy");
                    copy.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                    copy.Click += async (_, e) =>
                    {
                        await App.CopyTextAsync(selectedSingleFolder);
                        e.Handled = true;
                    };

                    var copyFullPath = new MenuItem();
                    copyFullPath.Header = App.Text("CopyPath");
                    copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
                    copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                    copyFullPath.Click += async (_, e) =>
                    {
                        await App.CopyTextAsync(Native.OS.GetAbsPath(repo.FullPath, selectedSingleFolder));
                        e.Handled = true;
                    };

                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(addToIgnore);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(history);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(copy);
                    menu.Items.Add(copyFullPath);
                }
            }

            return menu;
        }

        public ContextMenu CreateContextMenuForStagedChanges(ViewModels.WorkingCopy vm, string selectedSingleFolder)
        {
            var repo = vm.Repository;
            var selectedStaged = vm.SelectedStaged;
            if (repo == null || selectedStaged == null || selectedStaged.Count == 0)
                return null;

            var menu = new ContextMenu();

            MenuItem ai = null;
            var services = repo.GetPreferredOpenAIServices();
            if (services.Count > 0)
            {
                ai = new MenuItem();
                ai.Icon = App.CreateMenuIcon("Icons.AIAssist");
                ai.Header = App.Text("ChangeCM.GenerateCommitMessage");

                if (services.Count == 1)
                {
                    ai.Click += async (_, e) =>
                    {
                        await App.ShowDialog(new ViewModels.AIAssistant(repo, services[0], selectedStaged));
                        e.Handled = true;
                    };
                }
                else
                {
                    foreach (var service in services)
                    {
                        var dup = service;

                        var item = new MenuItem();
                        item.Header = service.Name;
                        item.Click += async (_, e) =>
                        {
                            await App.ShowDialog(new ViewModels.AIAssistant(repo, dup, selectedStaged));
                            e.Handled = true;
                        };

                        ai.Items.Add(item);
                    }
                }
            }

            var hasSelectedFolder = !string.IsNullOrEmpty(selectedSingleFolder);
            if (selectedStaged.Count == 1)
            {
                var change = selectedStaged[0];
                var path = Native.OS.GetAbsPath(repo.FullPath, change.Path);

                var openWithMerger = new MenuItem();
                openWithMerger.Header = App.Text("OpenInExternalMergeTool");
                openWithMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
                openWithMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
                openWithMerger.Click += (_, ev) =>
                {
                    vm.UseExternalDiffTool(change, false);
                    ev.Handled = true;
                };

                var explore = new MenuItem();
                explore.IsEnabled = File.Exists(path) || Directory.Exists(path);
                explore.Header = App.Text("RevealFile");
                explore.Icon = App.CreateMenuIcon("Icons.Explore");
                explore.Click += (_, e) =>
                {
                    var target = hasSelectedFolder ? Native.OS.GetAbsPath(repo.FullPath, selectedSingleFolder) : path;
                    Native.OS.OpenInFileManager(target, true);
                    e.Handled = true;
                };

                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.Unstage");
                unstage.Icon = App.CreateMenuIcon("Icons.File.Remove");
                unstage.Tag = "Enter/Space";
                unstage.Click += async (_, e) =>
                {
                    await vm.UnstageChangesAsync(selectedStaged, null);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.Stash");
                stash.Icon = App.CreateMenuIcon("Icons.Stashes.Add");
                stash.Click += (_, e) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.StashChanges(repo, selectedStaged));

                    e.Handled = true;
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

                    try
                    {
                        var storageFile = await storageProvider.SaveFilePickerAsync(options);
                        if (storageFile != null)
                            await vm.SaveChangesToPatchAsync(selectedStaged, false, storageFile.Path.LocalPath);
                    }
                    catch (Exception exception)
                    {
                        App.RaiseException(repo.FullPath, $"Failed to save as patch: {exception.Message}");
                    }

                    e.Handled = true;
                };

                TryAddOpenFileToContextMenu(menu, path);
                menu.Items.Add(openWithMerger);
                menu.Items.Add(explore);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(unstage);
                menu.Items.Add(stash);
                menu.Items.Add(patch);
                menu.Items.Add(new MenuItem() { Header = "-" });

                if (repo.IsLFSEnabled())
                {
                    var lfs = new MenuItem();
                    lfs.Header = App.Text("GitLFS");
                    lfs.Icon = App.CreateMenuIcon("Icons.LFS");

                    var lfsLock = new MenuItem();
                    lfsLock.Header = App.Text("GitLFS.Locks.Lock");
                    lfsLock.Icon = App.CreateMenuIcon("Icons.Lock");
                    lfsLock.IsEnabled = repo.Remotes.Count > 0;
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
                    lfsUnlock.IsEnabled = repo.Remotes.Count > 0;
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

                if (ai != null)
                {
                    menu.Items.Add(ai);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                if (hasSelectedFolder)
                {
                    var history = new MenuItem();
                    history.Header = App.Text("DirHistories");
                    history.Icon = App.CreateMenuIcon("Icons.Histories");
                    history.Click += (_, e) =>
                    {
                        App.ShowWindow(new ViewModels.DirHistories(repo, selectedSingleFolder));
                        e.Handled = true;
                    };

                    menu.Items.Add(history);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }
                else if (change.Index is not (Models.ChangeState.Added or Models.ChangeState.Renamed))
                {
                    var history = new MenuItem();
                    history.Header = App.Text("FileHistory");
                    history.Icon = App.CreateMenuIcon("Icons.Histories");
                    history.Click += (_, e) =>
                    {
                        App.ShowWindow(new ViewModels.FileHistories(repo.FullPath, change.Path));
                        e.Handled = true;
                    };

                    var blame = new MenuItem();
                    blame.Header = App.Text("Blame") + " (HEAD-only)";
                    blame.Icon = App.CreateMenuIcon("Icons.Blame");
                    blame.Click += async (_, e) =>
                    {
                        var commit = await new Commands.QuerySingleCommit(repo.FullPath, "HEAD").GetResultAsync();
                        App.ShowWindow(new ViewModels.Blame(repo.FullPath, change.Path, commit));
                        e.Handled = true;
                    };

                    menu.Items.Add(history);
                    menu.Items.Add(blame);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                TryToAddCustomActionsToContextMenu(repo, menu, change.Path);

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("CopyPath");
                copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
                copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                copyPath.Click += async (_, e) =>
                {
                    await App.CopyTextAsync(hasSelectedFolder ? selectedSingleFolder : change.Path);
                    e.Handled = true;
                };

                var copyFullPath = new MenuItem();
                copyFullPath.Header = App.Text("CopyFullPath");
                copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
                copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                copyFullPath.Click += async (_, e) =>
                {
                    var target = hasSelectedFolder ? Native.OS.GetAbsPath(repo.FullPath, selectedSingleFolder) : path;
                    await App.CopyTextAsync(target);
                    e.Handled = true;
                };

                menu.Items.Add(copyPath);
                menu.Items.Add(copyFullPath);
            }
            else
            {
                if (hasSelectedFolder)
                {
                    var dir = Path.Combine(repo.FullPath, selectedSingleFolder);
                    var explore = new MenuItem();
                    explore.IsEnabled = Directory.Exists(dir);
                    explore.Header = App.Text("RevealFile");
                    explore.Icon = App.CreateMenuIcon("Icons.Explore");
                    explore.Click += (_, e) =>
                    {
                        Native.OS.OpenInFileManager(dir, true);
                        e.Handled = true;
                    };

                    menu.Items.Add(explore);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.UnstageMulti", selectedStaged.Count);
                unstage.Icon = App.CreateMenuIcon("Icons.File.Remove");
                unstage.Tag = "Enter/Space";
                unstage.Click += async (_, e) =>
                {
                    await vm.UnstageChangesAsync(selectedStaged, null);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.StashMulti", selectedStaged.Count);
                stash.Icon = App.CreateMenuIcon("Icons.Stashes.Add");
                stash.Click += (_, e) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.StashChanges(repo, selectedStaged));

                    e.Handled = true;
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

                    try
                    {
                        var storageFile = await storageProvider.SaveFilePickerAsync(options);
                        if (storageFile != null)
                            await vm.SaveChangesToPatchAsync(selectedStaged, false, storageFile.Path.LocalPath);
                    }
                    catch (Exception exception)
                    {
                        App.RaiseException(repo.FullPath, $"Failed to save as patch: {exception.Message}");
                    }

                    e.Handled = true;
                };

                menu.Items.Add(unstage);
                menu.Items.Add(stash);
                menu.Items.Add(patch);

                if (ai != null)
                {
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(ai);
                }

                if (hasSelectedFolder)
                {
                    var history = new MenuItem();
                    history.Header = App.Text("DirHistories");
                    history.Icon = App.CreateMenuIcon("Icons.Histories");
                    history.Click += (_, e) =>
                    {
                        App.ShowWindow(new ViewModels.DirHistories(repo, selectedSingleFolder));
                        e.Handled = true;
                    };

                    var copyPath = new MenuItem();
                    copyPath.Header = App.Text("CopyPath");
                    copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
                    copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                    copyPath.Click += async (_, e) =>
                    {
                        await App.CopyTextAsync(selectedSingleFolder);
                        e.Handled = true;
                    };

                    var copyFullPath = new MenuItem();
                    copyFullPath.Header = App.Text("CopyFullPath");
                    copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
                    copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                    copyFullPath.Click += async (_, e) =>
                    {
                        await App.CopyTextAsync(Native.OS.GetAbsPath(repo.FullPath, selectedSingleFolder));
                        e.Handled = true;
                    };

                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(history);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(copyPath);
                    menu.Items.Add(copyFullPath);
                }
            }

            return menu;
        }

        private void TryAddOpenFileToContextMenu(ContextMenu menu, string fullpath)
        {
            var openWith = new MenuItem();
            openWith.Header = App.Text("Open");
            openWith.Icon = App.CreateMenuIcon("Icons.OpenWith");
            openWith.IsEnabled = File.Exists(fullpath);
            if (openWith.IsEnabled)
            {
                var defaultEditor = new MenuItem();
                defaultEditor.Header = App.Text("Open.SystemDefaultEditor");
                defaultEditor.Tag = OperatingSystem.IsMacOS() ? "⌘+O" : "Ctrl+O";
                defaultEditor.Click += (_, ev) =>
                {
                    Native.OS.OpenWithDefaultEditor(fullpath);
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
                        item.Click += (_, e) =>
                        {
                            tool.Open(fullpath);
                            e.Handled = true;
                        };

                        openWith.Items.Add(item);
                    }
                }
            }
            menu.Items.Add(openWith);
        }

        private void TryToAddCustomActionsToContextMenu(ViewModels.Repository repo, ContextMenu menu, string path)
        {
            var actions = repo.GetCustomActions(Models.CustomActionScope.File);
            if (actions.Count == 0)
                return;

            var target = new Models.CustomActionTargetFile(path, null);
            var custom = new MenuItem();
            custom.Header = App.Text("FileCM.CustomAction");
            custom.Icon = App.CreateMenuIcon("Icons.Action");

            foreach (var action in actions)
            {
                var (dup, label) = action;
                var item = new MenuItem();
                item.Icon = App.CreateMenuIcon("Icons.Action");
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
    }
}
