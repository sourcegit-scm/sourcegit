using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class ConflictContext
    {
        public Models.Change Change { get; set; }
    }

    public class WorkingCopy : ObservableObject
    {
        public bool IsStaging
        {
            get => _isStaging;
            private set => SetProperty(ref _isStaging, value);
        }

        public bool IsUnstaging
        {
            get => _isUnstaging;
            private set => SetProperty(ref _isUnstaging, value);
        }

        public bool IsCommitting
        {
            get => _isCommitting;
            private set => SetProperty(ref _isCommitting, value);
        }

        public bool UseAmend
        {
            get => _useAmend;
            set => SetProperty(ref _useAmend, value);
        }

        public List<Models.Change> Unstaged
        {
            get => _unstaged;
            private set => SetProperty(ref _unstaged, value);
        }

        public List<Models.Change> Staged
        {
            get => _staged;
            private set => SetProperty(ref _staged, value);
        }

        public int Count
        {
            get => _count;
        }

        public Models.Change SelectedUnstagedChange
        {
            get => _selectedUnstagedChange;
            set
            {
                if (SetProperty(ref _selectedUnstagedChange, value) && value != null)
                {
                    SelectedStagedChange = null;
                    SelectedStagedTreeNode = null;
                    SetDetail(value, true);
                }
            }
        }

        public Models.Change SelectedStagedChange
        {
            get => _selectedStagedChange;
            set
            {
                if (SetProperty(ref _selectedStagedChange, value) && value != null)
                {
                    SelectedUnstagedChange = null;
                    SelectedUnstagedTreeNode = null;
                    SetDetail(value, false);
                }
            }
        }

        public List<FileTreeNode> UnstagedTree
        {
            get => _unstagedTree;
            private set => SetProperty(ref _unstagedTree, value);
        }

        public List<FileTreeNode> StagedTree
        {
            get => _stagedTree;
            private set => SetProperty(ref _stagedTree, value);
        }

        public FileTreeNode SelectedUnstagedTreeNode
        {
            get => _selectedUnstagedTreeNode;
            set
            {
                if (SetProperty(ref _selectedUnstagedTreeNode, value))
                {
                    if (value == null)
                    {
                        SelectedUnstagedChange = null;
                    }
                    else
                    {
                        SelectedUnstagedChange = value.Backend as Models.Change;
                        SelectedStagedTreeNode = null;
                        SelectedStagedChange = null;

                        if (value.IsFolder)
                        {
                            SetDetail(null, true);
                        }
                    }
                }
            }
        }

        public FileTreeNode SelectedStagedTreeNode
        {
            get => _selectedStagedTreeNode;
            set
            {
                if (SetProperty(ref _selectedStagedTreeNode, value))
                {
                    if (value == null)
                    {
                        SelectedStagedChange = null;
                    }
                    else
                    {
                        SelectedStagedChange = value.Backend as Models.Change;
                        SelectedUnstagedTreeNode = null;
                        SelectedUnstagedChange = null;

                        if (value.IsFolder)
                        {
                            SetDetail(null, false);
                        }
                    }
                }
            }
        }

        public object DetailContext
        {
            get => _detailContext;
            private set => SetProperty(ref _detailContext, value);
        }

        public string CommitMessage
        {
            get => _commitMessage;
            set => SetProperty(ref _commitMessage, value);
        }

        public WorkingCopy(Repository repo)
        {
            _repo = repo;
        }

        public void Cleanup()
        {
            _repo = null;
            if (_unstaged != null)
                _unstaged.Clear();
            if (_staged != null)
                _staged.Clear();
            if (_unstagedTree != null)
                _unstagedTree.Clear();
            if (_stagedTree != null)
                _stagedTree.Clear();
            _selectedUnstagedChange = null;
            _selectedStagedChange = null;
            _selectedUnstagedTreeNode = null;
            _selectedStagedTreeNode = null;
            _detailContext = null;
            _commitMessage = string.Empty;
        }

        public bool SetData(List<Models.Change> changes)
        {
            var unstaged = new List<Models.Change>();
            var staged = new List<Models.Change>();

            var viewFile = string.Empty;
            var lastSelectedIsUnstaged = false;
            if (_selectedUnstagedChange != null)
            {
                viewFile = _selectedUnstagedChange.Path;
                lastSelectedIsUnstaged = true;
            }
            else if (_selectedStagedChange != null)
            {
                viewFile = _selectedStagedChange.Path;
            }

            var viewChange = null as Models.Change;
            var hasConflict = false;
            foreach (var c in changes)
            {
                if (c.Index == Models.ChangeState.Modified
                    || c.Index == Models.ChangeState.Added
                    || c.Index == Models.ChangeState.Deleted
                    || c.Index == Models.ChangeState.Renamed)
                {
                    staged.Add(c);
                    if (!lastSelectedIsUnstaged && c.Path == viewFile)
                    {
                        viewChange = c;
                    }
                }

                if (c.WorkTree != Models.ChangeState.None)
                {
                    unstaged.Add(c);
                    hasConflict |= c.IsConflit;
                    if (lastSelectedIsUnstaged && c.Path == viewFile)
                    {
                        viewChange = c;
                    }
                }
            }

            _count = changes.Count;

            var unstagedTree = FileTreeNode.Build(unstaged);
            var stagedTree = FileTreeNode.Build(staged);
            Dispatcher.UIThread.Invoke(() =>
            {
                _isLoadingData = true;
                Unstaged = unstaged;
                Staged = staged;
                UnstagedTree = unstagedTree;
                StagedTree = stagedTree;
                _isLoadingData = false;

                // Restore last selection states.
                if (viewChange != null)
                {
                    var scrollOffset = Vector.Zero;
                    if (_detailContext is DiffContext old)
                        scrollOffset = old.SyncScrollOffset;

                    if (lastSelectedIsUnstaged)
                    {
                        SelectedUnstagedChange = viewChange;
                        SelectedUnstagedTreeNode = FileTreeNode.SelectByPath(_unstagedTree, viewFile);
                    }
                    else
                    {
                        SelectedStagedChange = viewChange;
                        SelectedStagedTreeNode = FileTreeNode.SelectByPath(_stagedTree, viewFile);
                    }

                    if (_detailContext is DiffContext cur)
                        cur.SyncScrollOffset = scrollOffset;
                }
                else
                {
                    SelectedUnstagedChange = null;
                    SelectedUnstagedTreeNode = null;
                    SelectedStagedChange = null;
                    SelectedStagedTreeNode = null;
                    SetDetail(null, false);
                }
            });

            return hasConflict;
        }

        public void SetDetail(Models.Change change, bool isUnstaged)
        {
            if (_isLoadingData)
                return;

            if (change == null)
            {
                DetailContext = null;
            }
            else if (change.IsConflit)
            {
                DetailContext = new ConflictContext() { Change = change };
            }
            else
            {
                if (_detailContext is DiffContext previous)
                {
                    DetailContext = new DiffContext(_repo.FullPath, new Models.DiffOption(change, isUnstaged), previous);
                }
                else
                {
                    DetailContext = new DiffContext(_repo.FullPath, new Models.DiffOption(change, isUnstaged));
                }
            }
        }

        public async void StageChanges(List<Models.Change> changes)
        {
            if (_unstaged.Count == 0 || changes.Count == 0)
                return;

            SetDetail(null, true);
            IsStaging = true;
            _repo.SetWatcherEnabled(false);
            if (changes.Count == _unstaged.Count)
            {
                await Task.Run(() => new Commands.Add(_repo.FullPath).Exec());
            }
            else
            {
                for (int i = 0; i < changes.Count; i += 10)
                {
                    var count = Math.Min(10, changes.Count - i);
                    var step = changes.GetRange(i, count);
                    await Task.Run(() => new Commands.Add(_repo.FullPath, step).Exec());
                }
            }
            _repo.MarkWorkingCopyDirtyManually();
            _repo.SetWatcherEnabled(true);
            IsStaging = false;
        }

        public async void UnstageChanges(List<Models.Change> changes)
        {
            if (_staged.Count == 0 || changes.Count == 0)
                return;

            SetDetail(null, false);
            IsUnstaging = true;
            _repo.SetWatcherEnabled(false);
            if (changes.Count == _staged.Count)
            {
                await Task.Run(() => new Commands.Reset(_repo.FullPath).Exec());
            }
            else
            {
                for (int i = 0; i < changes.Count; i += 10)
                {
                    var count = Math.Min(10, changes.Count - i);
                    var step = changes.GetRange(i, count);
                    await Task.Run(() => new Commands.Reset(_repo.FullPath, step).Exec());
                }
            }
            _repo.MarkWorkingCopyDirtyManually();
            _repo.SetWatcherEnabled(true);
            IsUnstaging = false;
        }

        public void Discard(List<Models.Change> changes, bool isUnstaged)
        {
            if (PopupHost.CanCreatePopup())
            {
                if (isUnstaged)
                {
                    if (changes.Count == _unstaged.Count && _staged.Count == 0)
                    {
                        PopupHost.ShowPopup(new Discard(_repo));
                    }
                    else
                    {
                        PopupHost.ShowPopup(new Discard(_repo, changes, true));
                    }
                }
                else
                {
                    if (changes.Count == _staged.Count && _unstaged.Count == 0)
                    {
                        PopupHost.ShowPopup(new Discard(_repo));
                    }
                    else
                    {
                        PopupHost.ShowPopup(new Discard(_repo, changes, false));
                    }
                }
            }
        }

        public async void UseTheirs(List<Models.Change> changes)
        {
            var files = new List<string>();
            foreach (var change in changes)
            {
                if (change.IsConflit)
                    files.Add(change.Path);
            }

            _repo.SetWatcherEnabled(false);
            var succ = await Task.Run(() => new Commands.Checkout(_repo.FullPath).UseTheirs(files));
            if (succ)
            {
                await Task.Run(() => new Commands.Add(_repo.FullPath, changes).Exec());
            }
            _repo.MarkWorkingCopyDirtyManually();
            _repo.SetWatcherEnabled(true);
        }

        public async void UseMine(List<Models.Change> changes)
        {
            var files = new List<string>();
            foreach (var change in changes)
            {
                if (change.IsConflit)
                    files.Add(change.Path);
            }

            _repo.SetWatcherEnabled(false);
            var succ = await Task.Run(() => new Commands.Checkout(_repo.FullPath).UseMine(files));
            if (succ)
            {
                await Task.Run(() => new Commands.Add(_repo.FullPath, changes).Exec());
            }
            _repo.MarkWorkingCopyDirtyManually();
            _repo.SetWatcherEnabled(true);
        }

        public async void UseExternalMergeTool()
        {
            if (_detailContext is ConflictContext ctx)
            {
                var type = Preference.Instance.ExternalMergeToolType;
                var exec = Preference.Instance.ExternalMergeToolPath;

                var tool = Models.ExternalMerger.Supported.Find(x => x.Type == type);
                if (tool == null)
                {
                    App.RaiseException(_repo.FullPath, "Invalid merge tool in preference setting!");
                    return;
                }

                var args = tool.Type != 0 ? tool.Cmd : Preference.Instance.ExternalMergeToolCmd;

                _repo.SetWatcherEnabled(false);
                await Task.Run(() => Commands.MergeTool.OpenForMerge(_repo.FullPath, exec, args, ctx.Change.Path));
                _repo.SetWatcherEnabled(true);
            }
        }

        public async void DoCommit(bool autoPush)
        {
            if (!PopupHost.CanCreatePopup())
            {
                App.RaiseException(_repo.FullPath, "Repository has unfinished job! Please wait!");
                return;
            }

            if (_staged.Count == 0)
            {
                App.RaiseException(_repo.FullPath, "No files added to commit!");
                return;
            }

            if (string.IsNullOrWhiteSpace(_commitMessage))
            {
                App.RaiseException(_repo.FullPath, "Commit without message is NOT allowed!");
                return;
            }

            PushCommitMessage();

            SetDetail(null, false);
            IsCommitting = true;
            _repo.SetWatcherEnabled(false);
            var succ = await Task.Run(() => new Commands.Commit(_repo.FullPath, _commitMessage, _useAmend).Exec());
            if (succ)
            {
                CommitMessage = string.Empty;
                UseAmend = false;

                if (autoPush)
                {
                    PopupHost.ShowAndStartPopup(new Push(_repo, null));
                }
            }
            _repo.MarkWorkingCopyDirtyManually();
            _repo.SetWatcherEnabled(true);
            IsCommitting = false;
        }

        public ContextMenu CreateContextMenuForUnstagedChanges(List<Models.Change> changes)
        {
            if (changes.Count == 0)
                return null;

            var menu = new ContextMenu();
            if (changes.Count == 1)
            {
                var change = changes[0];
                var path = Path.GetFullPath(Path.Combine(_repo.FullPath, change.Path));

                var explore = new MenuItem();
                explore.Header = App.Text("RevealFile");
                explore.Icon = App.CreateMenuIcon("Icons.Folder.Open");
                explore.IsEnabled = File.Exists(path) || Directory.Exists(path);
                explore.Click += (_, e) =>
                {
                    Native.OS.OpenInFileManager(path, true);
                    e.Handled = true;
                };
                menu.Items.Add(explore);

                var openWith = new MenuItem();
                openWith.Header = App.Text("OpenWith");
                openWith.Icon = App.CreateMenuIcon("Icons.OpenWith");
                openWith.IsEnabled = File.Exists(path);
                openWith.Click += (_, e) =>
                {
                    Native.OS.OpenWithDefaultEditor(path);
                    e.Handled = true;
                };
                menu.Items.Add(openWith);
                menu.Items.Add(new MenuItem() { Header = "-" });

                if (change.IsConflit)
                {
                    var useTheirs = new MenuItem();
                    useTheirs.Icon = App.CreateMenuIcon("Icons.Incoming");
                    useTheirs.Header = App.Text("FileCM.UseTheirs");
                    useTheirs.Click += (_, e) =>
                    {
                        UseTheirs(changes);
                        e.Handled = true;
                    };
                    
                    var useMine = new MenuItem();
                    useMine.Icon = App.CreateMenuIcon("Icons.Local");
                    useMine.Header = App.Text("FileCM.UseMine");
                    useMine.Click += (_, e) =>
                    {
                        UseMine(changes);
                        e.Handled = true;
                    };

                    var openMerger = new MenuItem();
                    openMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
                    openMerger.Header = App.Text("FileCM.OpenWithExternalMerger");
                    openMerger.Click += (_, e) =>
                    {
                        UseExternalMergeTool();
                        e.Handled = true;
                    };

                    menu.Items.Add(useTheirs);
                    menu.Items.Add(useMine);
                    menu.Items.Add(openMerger);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }
                else
                {
                    var stage = new MenuItem();
                    stage.Header = App.Text("FileCM.Stage");
                    stage.Icon = App.CreateMenuIcon("Icons.File.Add");
                    stage.Click += (_, e) =>
                    {
                        StageChanges(changes);
                        e.Handled = true;
                    };

                    var discard = new MenuItem();
                    discard.Header = App.Text("FileCM.Discard");
                    discard.Icon = App.CreateMenuIcon("Icons.Undo");
                    discard.Click += (_, e) =>
                    {
                        Discard(changes, true);
                        e.Handled = true;
                    };

                    var stash = new MenuItem();
                    stash.Header = App.Text("FileCM.Stash");
                    stash.Icon = App.CreateMenuIcon("Icons.Stashes");
                    stash.Click += (_, e) =>
                    {
                        if (PopupHost.CanCreatePopup())
                        {
                            PopupHost.ShowPopup(new StashChanges(_repo, changes, false));
                        }
                        e.Handled = true;
                    };

                    var patch = new MenuItem();
                    patch.Header = App.Text("FileCM.SaveAsPatch");
                    patch.Icon = App.CreateMenuIcon("Icons.Diff");
                    patch.Click += async (_, e) =>
                    {
                        var topLevel = App.GetTopLevel();
                        if (topLevel == null)
                            return;

                        var options = new FilePickerSaveOptions();
                        options.Title = App.Text("FileCM.SaveAsPatch");
                        options.DefaultExtension = ".patch";
                        options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                        var storageFile = await topLevel.StorageProvider.SaveFilePickerAsync(options);
                        if (storageFile != null)
                        {
                            var succ = await Task.Run(() => Commands.SaveChangesAsPatch.Exec(_repo.FullPath, changes, true, storageFile.Path.LocalPath));
                            if (succ)
                                App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                        }

                        e.Handled = true;
                    };

                    var history = new MenuItem();
                    history.Header = App.Text("FileHistory");
                    history.Icon = App.CreateMenuIcon("Icons.Histories");
                    history.Click += (_, e) =>
                    {
                        var window = new Views.FileHistories() { DataContext = new FileHistories(_repo.FullPath, change.Path) };
                        window.Show();
                        e.Handled = true;
                    };

                    var assumeUnchanged = new MenuItem();
                    assumeUnchanged.Header = App.Text("FileCM.AssumeUnchanged");
                    assumeUnchanged.Icon = App.CreateMenuIcon("Icons.File.Ignore");
                    assumeUnchanged.IsEnabled = change.WorkTree != Models.ChangeState.Untracked;
                    assumeUnchanged.Click += (_, e) =>
                    {
                        new Commands.AssumeUnchanged(_repo.FullPath).Add(change.Path);
                        e.Handled = true;
                    };

                    menu.Items.Add(stage);
                    menu.Items.Add(discard);
                    menu.Items.Add(stash);
                    menu.Items.Add(patch);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(history);
                    menu.Items.Add(assumeUnchanged);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }
                
                var copy = new MenuItem();
                copy.Header = App.Text("CopyPath");
                copy.Icon = App.CreateMenuIcon("Icons.Copy");
                copy.Click += (_, e) =>
                {
                    App.CopyText(change.Path);
                    e.Handled = true;
                };                
                menu.Items.Add(copy);
            }
            else
            {
                var hasConflicts = false;
                var hasNoneConflicts = false;
                foreach (var change in changes)
                {
                    if (change.IsConflit)
                    {
                        hasConflicts = true;
                    }
                    else
                    {
                        hasNoneConflicts = true;
                    }
                }

                if (hasConflicts)
                {
                    if (hasNoneConflicts)
                    {
                        App.RaiseException(_repo.FullPath, "You have selected both non-conflict changes with conflicts!");
                        return null;
                    }

                    var useTheirs = new MenuItem();
                    useTheirs.Icon = App.CreateMenuIcon("Icons.Incoming");
                    useTheirs.Header = App.Text("FileCM.UseTheirs");
                    useTheirs.Click += (_, e) =>
                    {
                        UseTheirs(changes);
                        e.Handled = true;
                    };

                    var useMine = new MenuItem();
                    useMine.Icon = App.CreateMenuIcon("Icons.Local");
                    useMine.Header = App.Text("FileCM.UseMine");
                    useMine.Click += (_, e) =>
                    {
                        UseMine(changes);
                        e.Handled = true;
                    };

                    menu.Items.Add(useTheirs);
                    menu.Items.Add(useMine);
                    return menu;
                }

                var stage = new MenuItem();
                stage.Header = App.Text("FileCM.StageMulti", changes.Count);
                stage.Icon = App.CreateMenuIcon("Icons.File.Add");
                stage.Click += (_, e) =>
                {
                    StageChanges(changes);
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Text("FileCM.DiscardMulti", changes.Count);
                discard.Icon = App.CreateMenuIcon("Icons.Undo");
                discard.Click += (_, e) =>
                {
                    Discard(changes, true);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.StashMulti", changes.Count);
                stash.Icon = App.CreateMenuIcon("Icons.Stashes");
                stash.Click += (_, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                    {
                        PopupHost.ShowPopup(new StashChanges(_repo, changes, false));
                    }
                    e.Handled = true;
                };

                var patch = new MenuItem();
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Icon = App.CreateMenuIcon("Icons.Diff");
                patch.Click += async (o, e) =>
                {
                    var topLevel = App.GetTopLevel();
                    if (topLevel == null)
                        return;

                    var options = new FilePickerSaveOptions();
                    options.Title = App.Text("FileCM.SaveAsPatch");
                    options.DefaultExtension = ".patch";
                    options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                    var storageFile = await topLevel.StorageProvider.SaveFilePickerAsync(options);
                    if (storageFile != null)
                    {
                        var succ = await Task.Run(() => Commands.SaveChangesAsPatch.Exec(_repo.FullPath, changes, true, storageFile.Path.LocalPath));
                        if (succ)
                            App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                    }

                    e.Handled = true;
                };

                menu.Items.Add(stage);
                menu.Items.Add(discard);
                menu.Items.Add(stash);
                menu.Items.Add(patch);
            }

            return menu;
        }

        public ContextMenu CreateContextMenuForStagedChanges(List<Models.Change> changes)
        {
            if (changes.Count == 0)
                return null;

            var menu = new ContextMenu();
            if (changes.Count == 1)
            {
                var change = changes[0];
                var path = Path.GetFullPath(Path.Combine(_repo.FullPath, change.Path));

                var explore = new MenuItem();
                explore.IsEnabled = File.Exists(path) || Directory.Exists(path);
                explore.Header = App.Text("RevealFile");
                explore.Icon = App.CreateMenuIcon("Icons.Folder.Open");
                explore.Click += (o, e) =>
                {
                    Native.OS.OpenInFileManager(path, true);
                    e.Handled = true;
                };

                var openWith = new MenuItem();
                openWith.Header = App.Text("OpenWith");
                openWith.Icon = App.CreateMenuIcon("Icons.OpenWith");
                openWith.IsEnabled = File.Exists(path);
                openWith.Click += (_, e) =>
                {
                    Native.OS.OpenWithDefaultEditor(path);
                    e.Handled = true;
                };

                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.Unstage");
                unstage.Icon = App.CreateMenuIcon("Icons.File.Remove");
                unstage.Click += (o, e) =>
                {
                    UnstageChanges(changes);
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Text("FileCM.Discard");
                discard.Icon = App.CreateMenuIcon("Icons.Undo");
                discard.Click += (_, e) =>
                {
                    Discard(changes, false);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.Stash");
                stash.Icon = App.CreateMenuIcon("Icons.Stashes");
                stash.Click += (_, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                    {
                        PopupHost.ShowPopup(new StashChanges(_repo, changes, false));
                    }
                    e.Handled = true;
                };

                var patch = new MenuItem();
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Icon = App.CreateMenuIcon("Icons.Diff");
                patch.Click += async (o, e) =>
                {
                    var topLevel = App.GetTopLevel();
                    if (topLevel == null)
                        return;

                    var options = new FilePickerSaveOptions();
                    options.Title = App.Text("FileCM.SaveAsPatch");
                    options.DefaultExtension = ".patch";
                    options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                    var storageFile = await topLevel.StorageProvider.SaveFilePickerAsync(options);
                    if (storageFile != null)
                    {
                        var succ = await Task.Run(() => Commands.SaveChangesAsPatch.Exec(_repo.FullPath, changes, false, storageFile.Path.LocalPath));
                        if (succ)
                            App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                    }

                    e.Handled = true;
                };

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("CopyPath");
                copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
                copyPath.Click += (o, e) =>
                {
                    App.CopyText(change.Path);
                    e.Handled = true;
                };

                menu.Items.Add(explore);
                menu.Items.Add(openWith);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(unstage);
                menu.Items.Add(discard);
                menu.Items.Add(stash);
                menu.Items.Add(patch);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(copyPath);
            }
            else
            {
                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.UnstageMulti", changes.Count);
                unstage.Icon = App.CreateMenuIcon("Icons.File.Remove");
                unstage.Click += (o, e) =>
                {
                    UnstageChanges(changes);
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Text("FileCM.DiscardMulti", changes.Count);
                discard.Icon = App.CreateMenuIcon("Icons.Undo");
                discard.Click += (_, e) =>
                {
                    Discard(changes, false);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.StashMulti", changes.Count);
                stash.Icon = App.CreateMenuIcon("Icons.Stashes");
                stash.Click += (_, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                    {
                        PopupHost.ShowPopup(new StashChanges(_repo, changes, false));
                    }
                    e.Handled = true;
                };

                var patch = new MenuItem();
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Icon = App.CreateMenuIcon("Icons.Diff");
                patch.Click += async (_, e) =>
                {
                    var topLevel = App.GetTopLevel();
                    if (topLevel == null)
                        return;

                    var options = new FilePickerSaveOptions();
                    options.Title = App.Text("FileCM.SaveAsPatch");
                    options.DefaultExtension = ".patch";
                    options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                    var storageFile = await topLevel.StorageProvider.SaveFilePickerAsync(options);
                    if (storageFile != null)
                    {
                        var succ = await Task.Run(() => Commands.SaveChangesAsPatch.Exec(_repo.FullPath, changes, false, storageFile.Path.LocalPath));
                        if (succ)
                            App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                    }

                    e.Handled = true;
                };

                menu.Items.Add(unstage);
                menu.Items.Add(discard);
                menu.Items.Add(stash);
                menu.Items.Add(patch);
            }

            return menu;
        }

        public ContextMenu CreateContextMenuForCommitMessages()
        {
            var menu = new ContextMenu();
            if (_repo.CommitMessages.Count == 0)
            {
                var empty = new MenuItem();
                empty.Header = App.Text("WorkingCopy.NoCommitHistories");
                empty.IsEnabled = false;
                menu.Items.Add(empty);
                return menu;
            }

            var tip = new MenuItem();
            tip.Header = App.Text("WorkingCopy.HasCommitHistories");
            tip.IsEnabled = false;
            menu.Items.Add(tip);
            menu.Items.Add(new MenuItem() { Header = "-" });

            foreach (var message in _repo.CommitMessages)
            {
                var dump = message;

                var item = new MenuItem();
                item.Header = dump;
                item.Click += (o, e) =>
                {
                    CommitMessage = dump;
                    e.Handled = true;
                };

                menu.Items.Add(item);
            }

            return menu;
        }

        private void PushCommitMessage()
        {
            var existIdx = _repo.CommitMessages.IndexOf(CommitMessage);
            if (existIdx == 0)
            {
                return;
            }
            else if (existIdx > 0)
            {
                _repo.CommitMessages.Move(existIdx, 0);
                return;
            }

            if (_repo.CommitMessages.Count > 9)
            {
                _repo.CommitMessages.RemoveRange(9, _repo.CommitMessages.Count - 9);
            }

            _repo.CommitMessages.Insert(0, CommitMessage);
        }

        private Repository _repo = null;
        private bool _isLoadingData = false;
        private bool _isStaging = false;
        private bool _isUnstaging = false;
        private bool _isCommitting = false;
        private bool _useAmend = false;
        private List<Models.Change> _unstaged = null;
        private List<Models.Change> _staged = null;
        private Models.Change _selectedUnstagedChange = null;
        private Models.Change _selectedStagedChange = null;
        private int _count = 0;
        private List<FileTreeNode> _unstagedTree = null;
        private List<FileTreeNode> _stagedTree = null;
        private FileTreeNode _selectedUnstagedTreeNode = null;
        private FileTreeNode _selectedStagedTreeNode = null;
        private object _detailContext = null;
        private string _commitMessage = string.Empty;
    }
}
