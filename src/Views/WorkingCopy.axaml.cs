using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class WorkingCopy : UserControl
    {
        private const double SingleColumnThreshold = 720;
        private bool _isSingleColumn;
        private GridLength _expandedLeftWidth = new(300, GridUnitType.Pixel);

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
            if (width <= 0)
                return;

            var useSingleColumn = width < SingleColumnThreshold;
            if (useSingleColumn != _isSingleColumn)
                SetSingleColumnLayout(useSingleColumn);

            if (useSingleColumn)
                return;

            var leftWidth = Math.Max(220, width - 264);

            if (layout.WorkingCopyLeftWidth.Value - leftWidth > 1.0)
                layout.WorkingCopyLeftWidth = new GridLength(leftWidth, GridUnitType.Pixel);
        }

        private void SetSingleColumnLayout(bool enabled)
        {
            var columns = MainLayout.ColumnDefinitions;
            var rows = MainLayout.RowDefinitions;
            var layout = ViewModels.Preferences.Instance.Layout;

            _isSingleColumn = enabled;
            if (enabled)
            {
                if (layout.WorkingCopyLeftWidth.IsAbsolute && layout.WorkingCopyLeftWidth.Value >= 220)
                    _expandedLeftWidth = layout.WorkingCopyLeftWidth;

                columns[0].MinWidth = 0;
                columns[0].SetCurrentValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
                columns[1].Width = new GridLength(0);
                columns[2].MinWidth = 0;
                columns[2].Width = new GridLength(0);
                rows[0].Height = new GridLength(2, GridUnitType.Star);
                rows[1].Height = new GridLength(4, GridUnitType.Pixel);
                rows[2].Height = new GridLength(3, GridUnitType.Star);

                Grid.SetColumn(ChangesPanel, 0);
                Grid.SetRow(ChangesPanel, 0);
                Grid.SetColumn(LayoutSplitter, 0);
                Grid.SetRow(LayoutSplitter, 1);
                Grid.SetColumn(DetailsPanel, 0);
                Grid.SetRow(DetailsPanel, 2);
                LayoutSplitter.BorderThickness = new Thickness(0, 1, 0, 0);
                DetailsPanel.Margin = new Thickness(4, 0, 4, 4);
            }
            else
            {
                columns[0].MinWidth = 220;
                columns[0].SetCurrentValue(ColumnDefinition.WidthProperty, _expandedLeftWidth);
                columns[1].Width = new GridLength(4, GridUnitType.Pixel);
                columns[2].MinWidth = 260;
                columns[2].Width = new GridLength(1, GridUnitType.Star);
                rows[0].Height = new GridLength(1, GridUnitType.Star);
                rows[1].Height = new GridLength(0);
                rows[2].Height = new GridLength(0);

                Grid.SetColumn(ChangesPanel, 0);
                Grid.SetRow(ChangesPanel, 0);
                Grid.SetColumn(LayoutSplitter, 1);
                Grid.SetRow(LayoutSplitter, 0);
                Grid.SetColumn(DetailsPanel, 2);
                Grid.SetRow(DetailsPanel, 0);
                LayoutSplitter.BorderThickness = new Thickness(1, 0, 0, 0);
                DetailsPanel.Margin = new Thickness(0, 4, 4, 4);
                layout.WorkingCopyLeftWidth = _expandedLeftWidth;
            }
        }

        private async void OnOpenAssumeUnchanged(object sender, RoutedEventArgs e)
        {
            var repoView = this.FindAncestorOfType<Repository>();
            if (repoView is { DataContext: ViewModels.Repository repo })
                await this.ShowDialogAsync(new ViewModels.AssumeUnchangedManager(repo));

            e.Handled = true;
        }

        private void OnUnstagedContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy { Repository: { } repo, SelectedUnstaged: { Count: > 0 } selection } vm)
            {
                var menu = CreateContextMenuForUnstagedChanges(repo, vm, selection);
                menu?.Open(sender as Control);
                e.Handled = true;
            }
        }

        private void OnStagedContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy { Repository: { } repo, SelectedStaged: { Count: > 0 } selection } vm)
            {
                var menu = CreateContextMenuForStagedChanges(repo, vm, selection);
                menu?.Open(sender as Control);
                e.Handled = true;
            }
        }

        private async void OnUnstagedChangeDoubleTapped(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy { SelectedUnstaged: { Count: > 0 } selection } vm)
            {
                var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                await vm.StageChangesAsync(selection.Changes, next);
                UnstagedChangesView.TakeFocus();
                e.Handled = true;
            }
        }

        private async void OnStagedChangeDoubleTapped(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy { SelectedStaged: { Count: > 0 } selection } vm)
            {
                var next = StagedChangesView.GetNextChangeWithoutSelection();
                await vm.UnstageChangesAsync(selection.Changes, next);
                StagedChangesView.TakeFocus();
                e.Handled = true;
            }
        }

        private async void OnUnstagedKeyDown(object _, KeyEventArgs e)
        {
            var cmdKey = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

            if (DataContext is ViewModels.WorkingCopy { SelectedUnstaged: { Count: > 0 } selection } vm)
            {
                var changes = selection.Changes;

                if (e.Key is Key.Space or Key.Enter)
                {
                    var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                    await vm.StageChangesAsync(changes, next);
                    UnstagedChangesView.TakeFocus();
                    e.Handled = true;
                }
                else if (e.Key is Key.Delete or Key.Back)
                {
                    vm.Discard(changes);
                    e.Handled = true;
                }
                else if (e.Key is Key.O && e.KeyModifiers == cmdKey && changes.Count == 1)
                {
                    var change = changes[0];
                    var fullpath = Native.OS.GetAbsPath(vm.Repository.FullPath, change.Path);
                    if (File.Exists(fullpath))
                        Native.OS.OpenWithDefaultEditor(fullpath);
                    e.Handled = true;
                }
                else if (e.Key is Key.C && e.KeyModifiers.HasFlag(cmdKey))
                {
                    var builder = new StringBuilder();
                    var copyAbsPath = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                    if (selection.IsSingleFolder)
                    {
                        builder.Append(copyAbsPath ? Native.OS.GetAbsPath(vm.Repository.FullPath, selection.SingleFolderPath) : selection.SingleFolderPath);
                    }
                    else if (changes.Count == 1)
                    {
                        var change = changes[0];
                        builder.Append(copyAbsPath ? Native.OS.GetAbsPath(vm.Repository.FullPath, change.Path) : change.Path);
                    }
                    else
                    {
                        foreach (var c in changes)
                            builder.AppendLine(copyAbsPath ? Native.OS.GetAbsPath(vm.Repository.FullPath, c.Path) : c.Path);
                    }

                    if (builder.Length > 0)
                    {
                        await this.CopyTextAsync(builder.ToString());
                        e.Handled = true;
                    }
                }
            }
            else if (e.Key is Key.F && e.KeyModifiers == cmdKey)
            {
                LocalChangesSearchBox.Focus();
                e.Handled = true;
            }
        }

        private async void OnStagedKeyDown(object _, KeyEventArgs e)
        {
            var cmdKey = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

            if (DataContext is ViewModels.WorkingCopy { SelectedStaged: { Count: > 0 } selection } vm)
            {
                var changes = selection.Changes;

                if (e.Key is Key.Space or Key.Enter)
                {
                    var next = StagedChangesView.GetNextChangeWithoutSelection();
                    await vm.UnstageChangesAsync(changes, next);
                    StagedChangesView.TakeFocus();
                    e.Handled = true;
                }
                else if (e.Key is Key.O && e.KeyModifiers == cmdKey && changes.Count == 1)
                {
                    var change = changes[0];
                    var fullpath = Native.OS.GetAbsPath(vm.Repository.FullPath, change.Path);
                    if (File.Exists(fullpath))
                        Native.OS.OpenWithDefaultEditor(fullpath);
                    e.Handled = true;
                }
                else if (e.Key is Key.C && e.KeyModifiers.HasFlag(cmdKey))
                {
                    var builder = new StringBuilder();
                    var copyAbsPath = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                    if (selection.IsSingleFolder)
                    {
                        builder.Append(copyAbsPath ? Native.OS.GetAbsPath(vm.Repository.FullPath, selection.SingleFolderPath) : selection.SingleFolderPath);
                    }
                    else if (changes.Count == 1)
                    {
                        var change = changes[0];
                        builder.Append(copyAbsPath ? Native.OS.GetAbsPath(vm.Repository.FullPath, change.Path) : change.Path);
                    }
                    else
                    {
                        foreach (var c in changes)
                            builder.AppendLine(copyAbsPath ? Native.OS.GetAbsPath(vm.Repository.FullPath, c.Path) : c.Path);
                    }

                    if (builder.Length > 0)
                    {
                        await this.CopyTextAsync(builder.ToString());
                        e.Handled = true;
                    }
                }
            }
            else if (e.Key is Key.F && e.KeyModifiers == cmdKey)
            {
                LocalChangesSearchBox.Focus();
                e.Handled = true;
            }
        }

        private async void OnStageSelectedButtonClicked(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy { SelectedUnstaged: { Count: > 0 } selection } vm)
            {
                var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                await vm.StageChangesAsync(selection.Changes, next);
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
            if (DataContext is ViewModels.WorkingCopy { SelectedStaged: { Count: > 0 } selection } vm)
            {
                var next = StagedChangesView.GetNextChangeWithoutSelection();
                await vm.UnstageChangesAsync(selection.Changes, next);
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
            if (App.GetLauncher() is { CommandPalette: { } } launcher)
                return;

            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.CommitAsync(false, false);

            e.Handled = true;
        }

        private async void OnCommitWithAutoStage(object _, RoutedEventArgs e)
        {
            if (App.GetLauncher() is { CommandPalette: { } } launcher)
                return;

            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.CommitAsync(true, false);

            e.Handled = true;
        }

        private async void OnCommitWithPush(object _, RoutedEventArgs e)
        {
            if (App.GetLauncher() is { CommandPalette: { } } launcher)
                return;

            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.CommitAsync(false, true);

            e.Handled = true;
        }

        private ContextMenu CreateContextMenuForUnstagedChanges(ViewModels.Repository repo, ViewModels.WorkingCopy vm, ViewModels.ChangeSelection selection)
        {
            var changes = selection.Changes;
            var menu = new ContextMenu();

            if (changes.Count == 1)
            {
                var change = changes[0];
                var path = Native.OS.GetAbsPath(repo.FullPath, change.Path);

                if (!change.IsConflicted && !selection.HasFolder)
                {
                    TryAddOpenFileToContextMenu(menu, path);

                    var diffWithMerger = new MenuItem();
                    diffWithMerger.Header = App.Text("OpenInExternalMergeTool");
                    diffWithMerger.Icon = this.CreateMenuIcon("Icons.OpenWith");
                    diffWithMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
                    diffWithMerger.Click += (_, ev) =>
                    {
                        vm.UseExternalDiffTool(change, true);
                        ev.Handled = true;
                    };

                    menu.Items.Add(diffWithMerger);
                }

                if (!selection.HasFolder || selection.IsSingleFolder)
                {
                    var absPath = selection.IsSingleFolder ? Native.OS.GetAbsPath(repo.FullPath, selection.SingleFolderPath) : path;
                    var explore = new MenuItem();
                    explore.Header = App.Text("RevealFile");
                    explore.Icon = this.CreateMenuIcon("Icons.Explore");
                    explore.IsEnabled = Path.Exists(absPath);
                    explore.Click += (_, e) =>
                    {
                        Native.OS.OpenInFileManager(absPath);
                        e.Handled = true;
                    };
                    menu.Items.Add(explore);
                }

                if (menu.Items.Count > 0)
                    menu.Items.Add(new MenuItem() { Header = "-" });

                if (change.IsConflicted)
                {
                    var useTheirs = new MenuItem();
                    useTheirs.Icon = this.CreateMenuIcon("Icons.Incoming");
                    useTheirs.Click += async (_, e) =>
                    {
                        await vm.UseTheirsAsync(changes);
                        e.Handled = true;
                    };

                    var useMine = new MenuItem();
                    useMine.Icon = this.CreateMenuIcon("Icons.Local");
                    useMine.Click += async (_, e) =>
                    {
                        await vm.UseMineAsync(changes);
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

                    if (change.ConflictReason is Models.ConflictReason.BothAdded or Models.ConflictReason.BothModified && !Directory.Exists(path))
                    {
                        var mergeBuiltin = new MenuItem();
                        mergeBuiltin.Header = App.Text("ChangeCM.Merge");
                        mergeBuiltin.Icon = this.CreateMenuIcon("Icons.Conflict");
                        mergeBuiltin.Click += async (_, e) =>
                        {
                            var head = await new Commands.QuerySingleCommit(repo.FullPath, "HEAD").GetResultAsync();
                            this.ShowWindow(new ViewModels.MergeConflictEditor(repo, head, change.Path));
                            e.Handled = true;
                        };

                        var mergeExternal = new MenuItem();
                        mergeExternal.Header = App.Text("ChangeCM.MergeExternal");
                        mergeExternal.Icon = this.CreateMenuIcon("Icons.OpenWith");
                        mergeExternal.Click += async (_, e) =>
                        {
                            await vm.UseExternalMergeToolAsync(change);
                            e.Handled = true;
                        };

                        menu.Items.Add(mergeBuiltin);
                        menu.Items.Add(mergeExternal);
                    }

                    menu.Items.Add(new MenuItem() { Header = "-" });
                }
                else
                {
                    var stage = new MenuItem();
                    stage.Header = App.Text("FileCM.Stage");
                    stage.Icon = this.CreateMenuIcon("Icons.File.Add");
                    stage.Tag = "Enter/Space";
                    stage.Click += async (_, e) =>
                    {
                        await vm.StageChangesAsync(changes, null);
                        e.Handled = true;
                    };

                    var discard = new MenuItem();
                    discard.Header = App.Text("FileCM.Discard");
                    discard.Icon = this.CreateMenuIcon("Icons.Undo");
                    discard.Tag = "Back/Delete";
                    discard.Click += (_, e) =>
                    {
                        vm.Discard(changes);
                        e.Handled = true;
                    };

                    var stash = new MenuItem();
                    stash.Header = App.Text("FileCM.Stash");
                    stash.Icon = this.CreateMenuIcon("Icons.Stashes.Add");
                    stash.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.StashChanges(repo, changes));

                        e.Handled = true;
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
                                await vm.SaveChangesToPatchAsync(changes, true, storageFile.Path.LocalPath);
                        }
                        catch (Exception exception)
                        {
                            repo.SendNotification($"Failed to save as patch: {exception.Message}", true);
                        }

                        e.Handled = true;
                    };

                    var assumeUnchanged = new MenuItem();
                    assumeUnchanged.Header = App.Text("FileCM.AssumeUnchanged");
                    assumeUnchanged.Icon = this.CreateMenuIcon("Icons.File.Ignore");
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
                    if (selection.IsSingleFolder)
                    {
                        var addToIgnore = new MenuItem();
                        addToIgnore.Header = App.Text("WorkingCopy.AddToGitIgnore");
                        addToIgnore.Icon = this.CreateMenuIcon("Icons.GitIgnore");

                        var ignoreFolder = new MenuItem();
                        ignoreFolder.Header = App.Text("WorkingCopy.AddToGitIgnore.InFolder");
                        ignoreFolder.Click += (_, e) =>
                        {
                            if (repo.CanCreatePopup())
                                repo.ShowPopup(new ViewModels.AddToIgnore(repo, $"{selection.SingleFolderPath}/"));
                            e.Handled = true;
                        };
                        addToIgnore.Items.Add(ignoreFolder);

                        menu.Items.Add(addToIgnore);
                        hasExtra = true;
                    }
                    else if (!selection.HasFolder && change.WorkTree == Models.ChangeState.Untracked)
                    {
                        var addToIgnore = new MenuItem();
                        addToIgnore.Header = App.Text("WorkingCopy.AddToGitIgnore");
                        addToIgnore.Icon = this.CreateMenuIcon("Icons.GitIgnore");

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

                        if (!isRooted)
                        {
                            var untrackedInSameFolder = new MenuItem();
                            untrackedInSameFolder.Header = App.Text("WorkingCopy.AddToGitIgnore.UntrackedInSameFolder");
                            untrackedInSameFolder.Click += (_, e) =>
                            {
                                var dir = Path.GetDirectoryName(change.Path)!.Replace('\\', '/').TrimEnd('/');
                                if (repo.CanCreatePopup())
                                    repo.ShowPopup(new ViewModels.AddToIgnore(repo, $"{dir}/"));
                                e.Handled = true;
                            };
                            addToIgnore.Items.Add(untrackedInSameFolder);
                        }

                        menu.Items.Add(addToIgnore);
                        hasExtra = true;
                    }

                    if (!selection.HasFolder && File.Exists(path) && repo.IsLFSEnabled())
                    {
                        var lfs = new MenuItem();
                        lfs.Header = App.Text("GitLFS");
                        lfs.Icon = this.CreateMenuIcon("Icons.LFS");

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
                        lfsLock.Icon = this.CreateMenuIcon("Icons.Lock");
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
                        lfsUnlock.Icon = this.CreateMenuIcon("Icons.Unlock");
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

                if (selection.IsSingleFolder)
                {
                    var history = new MenuItem();
                    history.Header = App.Text("DirHistories");
                    history.Icon = this.CreateMenuIcon("Icons.Histories");
                    history.Click += (_, e) =>
                    {
                        this.ShowWindow(new ViewModels.DirHistories(repo, selection.SingleFolderPath));
                        e.Handled = true;
                    };

                    menu.Items.Add(history);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }
                else if (change.WorkTree is not (Models.ChangeState.Untracked or Models.ChangeState.Added))
                {
                    var history = new MenuItem();
                    history.Header = App.Text("FileHistory");
                    history.Icon = this.CreateMenuIcon("Icons.Histories");
                    history.Click += (_, e) =>
                    {
                        this.ShowWindow(new ViewModels.FileHistories(repo.FullPath, change.Path));
                        e.Handled = true;
                    };

                    var blame = new MenuItem();
                    blame.Header = App.Text("Blame") + " (HEAD-only)";
                    blame.Icon = this.CreateMenuIcon("Icons.Blame");
                    blame.Click += async (_, ev) =>
                    {
                        var commit = await new Commands.QuerySingleCommit(repo.FullPath, "HEAD").GetResultAsync();
                        this.ShowWindow(new ViewModels.Blame(repo.FullPath, change.Path, commit));
                        ev.Handled = true;
                    };

                    menu.Items.Add(history);
                    menu.Items.Add(blame);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                TryToAddCustomActionsToContextMenu(repo, menu, change.Path);

                var copy = new MenuItem();
                copy.Header = App.Text("CopyPath");
                copy.Icon = this.CreateMenuIcon("Icons.Copy");
                copy.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                copy.Click += async (_, e) =>
                {
                    await this.CopyTextAsync(selection.IsSingleFolder ? selection.SingleFolderPath : change.Path);
                    e.Handled = true;
                };

                var copyFullPath = new MenuItem();
                copyFullPath.Header = App.Text("CopyFullPath");
                copyFullPath.Icon = this.CreateMenuIcon("Icons.Copy");
                copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                copyFullPath.Click += async (_, e) =>
                {
                    await this.CopyTextAsync(selection.IsSingleFolder ? Native.OS.GetAbsPath(repo.FullPath, selection.SingleFolderPath) : path);
                    e.Handled = true;
                };

                menu.Items.Add(copy);
                menu.Items.Add(copyFullPath);
            }
            else
            {
                var hasConflicts = false;
                var hasNonConflicts = false;
                foreach (var change in changes)
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
                        repo.SendNotification("Selection contains both conflict and non-conflict changes!", true);
                        return null;
                    }

                    var useTheirs = new MenuItem();
                    useTheirs.Icon = this.CreateMenuIcon("Icons.Incoming");
                    useTheirs.Click += async (_, e) =>
                    {
                        await vm.UseTheirsAsync(changes);
                        e.Handled = true;
                    };

                    var useMine = new MenuItem();
                    useMine.Icon = this.CreateMenuIcon("Icons.Local");
                    useMine.Click += async (_, e) =>
                    {
                        await vm.UseMineAsync(changes);
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

                if (selection.IsSingleFolder)
                {
                    var dir = Path.Combine(repo.FullPath, selection.SingleFolderPath);
                    var explore = new MenuItem();
                    explore.Header = App.Text("RevealFile");
                    explore.Icon = this.CreateMenuIcon("Icons.Explore");
                    explore.IsEnabled = Directory.Exists(dir);
                    explore.Click += (_, e) =>
                    {
                        Native.OS.OpenInFileManager(dir);
                        e.Handled = true;
                    };
                    menu.Items.Add(explore);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                var stage = new MenuItem();
                stage.Header = App.Text("FileCM.StageMulti", changes.Count);
                stage.Icon = this.CreateMenuIcon("Icons.File.Add");
                stage.Tag = "Enter/Space";
                stage.Click += async (_, e) =>
                {
                    await vm.StageChangesAsync(changes, null);
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Text("FileCM.DiscardMulti", changes.Count);
                discard.Icon = this.CreateMenuIcon("Icons.Undo");
                discard.Tag = "Back/Delete";
                discard.Click += (_, e) =>
                {
                    vm.Discard(changes);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.StashMulti", changes.Count);
                stash.Icon = this.CreateMenuIcon("Icons.Stashes.Add");
                stash.Click += (_, e) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.StashChanges(repo, changes));

                    e.Handled = true;
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
                            await vm.SaveChangesToPatchAsync(changes, true, storageFile.Path.LocalPath);
                    }
                    catch (Exception exception)
                    {
                        repo.SendNotification($"Failed to save as patch: {exception.Message}", true);
                    }

                    e.Handled = true;
                };

                menu.Items.Add(stage);
                menu.Items.Add(discard);
                menu.Items.Add(stash);
                menu.Items.Add(patch);

                if (selection.IsSingleFolder)
                {
                    var ignoreFolder = new MenuItem();
                    ignoreFolder.Header = App.Text("WorkingCopy.AddToGitIgnore.InFolder");
                    ignoreFolder.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.AddToIgnore(repo, $"{selection.SingleFolderPath}/"));
                        e.Handled = true;
                    };

                    var addToIgnore = new MenuItem();
                    addToIgnore.Header = App.Text("WorkingCopy.AddToGitIgnore");
                    addToIgnore.Icon = this.CreateMenuIcon("Icons.GitIgnore");
                    addToIgnore.Items.Add(ignoreFolder);

                    var history = new MenuItem();
                    history.Header = App.Text("DirHistories");
                    history.Icon = this.CreateMenuIcon("Icons.Histories");
                    history.Click += (_, e) =>
                    {
                        this.ShowWindow(new ViewModels.DirHistories(repo, selection.SingleFolderPath));
                        e.Handled = true;
                    };

                    var copy = new MenuItem();
                    copy.Header = App.Text("CopyPath");
                    copy.Icon = this.CreateMenuIcon("Icons.Copy");
                    copy.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                    copy.Click += async (_, e) =>
                    {
                        await this.CopyTextAsync(selection.SingleFolderPath);
                        e.Handled = true;
                    };

                    var copyFullPath = new MenuItem();
                    copyFullPath.Header = App.Text("CopyPath");
                    copyFullPath.Icon = this.CreateMenuIcon("Icons.Copy");
                    copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                    copyFullPath.Click += async (_, e) =>
                    {
                        await this.CopyTextAsync(Native.OS.GetAbsPath(repo.FullPath, selection.SingleFolderPath));
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

        public ContextMenu CreateContextMenuForStagedChanges(ViewModels.Repository repo, ViewModels.WorkingCopy vm, ViewModels.ChangeSelection selection)
        {
            var changes = selection.Changes;
            var menu = new ContextMenu();

            MenuItem ai = null;
            var services = repo.GetPreferredOpenAIServices();
            if (services.Count > 0)
            {
                ai = new MenuItem();
                ai.Icon = this.CreateMenuIcon("Icons.AIAssist");
                ai.Header = App.Text("ChangeCM.GenerateCommitMessage");

                if (services.Count == 1)
                {
                    ai.Click += (_, e) =>
                    {
                        DoOpenAIAssistant(repo, services[0], changes);
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
                        item.Click += (_, e) =>
                        {
                            DoOpenAIAssistant(repo, dup, changes);
                            e.Handled = true;
                        };

                        ai.Items.Add(item);
                    }
                }
            }

            if (changes.Count == 1)
            {
                var change = changes[0];
                var path = Native.OS.GetAbsPath(repo.FullPath, change.Path);

                if (!selection.HasFolder)
                {
                    TryAddOpenFileToContextMenu(menu, path);

                    var openWithMerger = new MenuItem();
                    openWithMerger.Header = App.Text("OpenInExternalMergeTool");
                    openWithMerger.Icon = this.CreateMenuIcon("Icons.OpenWith");
                    openWithMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
                    openWithMerger.Click += (_, ev) =>
                    {
                        vm.UseExternalDiffTool(change, false);
                        ev.Handled = true;
                    };
                    menu.Items.Add(openWithMerger);
                }

                if (!selection.HasFolder || selection.IsSingleFolder)
                {
                    var absPath = selection.IsSingleFolder ? Native.OS.GetAbsPath(repo.FullPath, selection.SingleFolderPath) : path;
                    var explore = new MenuItem();
                    explore.IsEnabled = File.Exists(path) || Directory.Exists(path);
                    explore.Header = App.Text("RevealFile");
                    explore.Icon = this.CreateMenuIcon("Icons.Explore");
                    explore.IsEnabled = Path.Exists(absPath);
                    explore.Click += (_, e) =>
                    {
                        Native.OS.OpenInFileManager(absPath);
                        e.Handled = true;
                    };
                    menu.Items.Add(explore);
                }

                if (menu.Items.Count > 0)
                    menu.Items.Add(new MenuItem() { Header = "-" });

                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.Unstage");
                unstage.Icon = this.CreateMenuIcon("Icons.File.Remove");
                unstage.Tag = "Enter/Space";
                unstage.Click += async (_, e) =>
                {
                    await vm.UnstageChangesAsync(changes, null);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.Stash");
                stash.Icon = this.CreateMenuIcon("Icons.Stashes.Add");
                stash.IsEnabled = !vm.UseAmend;
                stash.Click += (_, e) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.StashChanges(repo, changes));

                    e.Handled = true;
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
                            await vm.SaveChangesToPatchAsync(changes, false, storageFile.Path.LocalPath);
                    }
                    catch (Exception exception)
                    {
                        repo.SendNotification($"Failed to save as patch: {exception.Message}", true);
                    }

                    e.Handled = true;
                };

                menu.Items.Add(unstage);
                menu.Items.Add(stash);
                menu.Items.Add(patch);
                menu.Items.Add(new MenuItem() { Header = "-" });

                if (!selection.HasFolder && File.Exists(path) && repo.IsLFSEnabled())
                {
                    var lfs = new MenuItem();
                    lfs.Header = App.Text("GitLFS");
                    lfs.Icon = this.CreateMenuIcon("Icons.LFS");

                    var lfsLock = new MenuItem();
                    lfsLock.Header = App.Text("GitLFS.Locks.Lock");
                    lfsLock.Icon = this.CreateMenuIcon("Icons.Lock");
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
                    lfsUnlock.Icon = this.CreateMenuIcon("Icons.Unlock");
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

                if (selection.IsSingleFolder)
                {
                    var history = new MenuItem();
                    history.Header = App.Text("DirHistories");
                    history.Icon = this.CreateMenuIcon("Icons.Histories");
                    history.Click += (_, e) =>
                    {
                        this.ShowWindow(new ViewModels.DirHistories(repo, selection.SingleFolderPath));
                        e.Handled = true;
                    };

                    menu.Items.Add(history);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }
                else if (change.Index is not (Models.ChangeState.Added or Models.ChangeState.Renamed))
                {
                    var history = new MenuItem();
                    history.Header = App.Text("FileHistory");
                    history.Icon = this.CreateMenuIcon("Icons.Histories");
                    history.Click += (_, e) =>
                    {
                        this.ShowWindow(new ViewModels.FileHistories(repo.FullPath, change.Path));
                        e.Handled = true;
                    };

                    var blame = new MenuItem();
                    blame.Header = App.Text("Blame") + " (HEAD-only)";
                    blame.Icon = this.CreateMenuIcon("Icons.Blame");
                    blame.Click += async (_, e) =>
                    {
                        var commit = await new Commands.QuerySingleCommit(repo.FullPath, "HEAD").GetResultAsync();
                        this.ShowWindow(new ViewModels.Blame(repo.FullPath, change.Path, commit));
                        e.Handled = true;
                    };

                    menu.Items.Add(history);
                    menu.Items.Add(blame);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                TryToAddCustomActionsToContextMenu(repo, menu, change.Path);

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("CopyPath");
                copyPath.Icon = this.CreateMenuIcon("Icons.Copy");
                copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                copyPath.Click += async (_, e) =>
                {
                    await this.CopyTextAsync(selection.IsSingleFolder ? selection.SingleFolderPath : change.Path);
                    e.Handled = true;
                };

                var copyFullPath = new MenuItem();
                copyFullPath.Header = App.Text("CopyFullPath");
                copyFullPath.Icon = this.CreateMenuIcon("Icons.Copy");
                copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                copyFullPath.Click += async (_, e) =>
                {
                    var target = selection.IsSingleFolder ? Native.OS.GetAbsPath(repo.FullPath, selection.SingleFolderPath) : path;
                    await this.CopyTextAsync(target);
                    e.Handled = true;
                };

                menu.Items.Add(copyPath);
                menu.Items.Add(copyFullPath);
            }
            else
            {
                if (selection.IsSingleFolder)
                {
                    var dir = Native.OS.GetAbsPath(repo.FullPath, selection.SingleFolderPath);
                    var explore = new MenuItem();
                    explore.IsEnabled = Directory.Exists(dir);
                    explore.Header = App.Text("RevealFile");
                    explore.Icon = this.CreateMenuIcon("Icons.Explore");
                    explore.Click += (_, e) =>
                    {
                        Native.OS.OpenInFileManager(dir);
                        e.Handled = true;
                    };

                    menu.Items.Add(explore);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.UnstageMulti", changes.Count);
                unstage.Icon = this.CreateMenuIcon("Icons.File.Remove");
                unstage.Tag = "Enter/Space";
                unstage.Click += async (_, e) =>
                {
                    await vm.UnstageChangesAsync(changes, null);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.StashMulti", changes.Count);
                stash.Icon = this.CreateMenuIcon("Icons.Stashes.Add");
                stash.IsEnabled = !vm.UseAmend;
                stash.Click += (_, e) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.StashChanges(repo, changes));

                    e.Handled = true;
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
                            await vm.SaveChangesToPatchAsync(changes, false, storageFile.Path.LocalPath);
                    }
                    catch (Exception exception)
                    {
                        repo.SendNotification($"Failed to save as patch: {exception.Message}", true);
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

                if (selection.IsSingleFolder)
                {
                    var history = new MenuItem();
                    history.Header = App.Text("DirHistories");
                    history.Icon = this.CreateMenuIcon("Icons.Histories");
                    history.Click += (_, e) =>
                    {
                        this.ShowWindow(new ViewModels.DirHistories(repo, selection.SingleFolderPath));
                        e.Handled = true;
                    };

                    var copyPath = new MenuItem();
                    copyPath.Header = App.Text("CopyPath");
                    copyPath.Icon = this.CreateMenuIcon("Icons.Copy");
                    copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                    copyPath.Click += async (_, e) =>
                    {
                        await this.CopyTextAsync(selection.SingleFolderPath);
                        e.Handled = true;
                    };

                    var copyFullPath = new MenuItem();
                    copyFullPath.Header = App.Text("CopyFullPath");
                    copyFullPath.Icon = this.CreateMenuIcon("Icons.Copy");
                    copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                    copyFullPath.Click += async (_, e) =>
                    {
                        await this.CopyTextAsync(Native.OS.GetAbsPath(repo.FullPath, selection.SingleFolderPath));
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
            openWith.Icon = this.CreateMenuIcon("Icons.OpenWith");
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
                            tool.Launch(fullpath.Quoted());
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
            custom.Icon = this.CreateMenuIcon("Icons.Action");

            foreach (var action in actions)
            {
                var (dup, label) = action;
                var item = new MenuItem();
                item.Icon = this.CreateMenuIcon("Icons.Action");
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

        private void DoOpenAIAssistant(ViewModels.Repository repo, AI.Service serivce, List<Models.Change> changes)
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
                return;

            var assistant = new ViewModels.AIAssistant(repo, serivce, changes);
            var view = new AIAssistant() { DataContext = assistant };
            view.Show(owner);
        }
    }
}
