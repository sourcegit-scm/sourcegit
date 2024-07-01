﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class ConflictContext : ObservableObject
    {
        public bool IsResolved
        {
            get => _isResolved;
            set => SetProperty(ref _isResolved, value);
        }

        public ConflictContext(string repo, Models.Change change)
        {
            Task.Run(() =>
            {
                var result = new Commands.IsConflictResolved(repo, change).Result();
                Dispatcher.UIThread.Post(() =>
                {
                    IsResolved = result;
                });
            });
        }

        private bool _isResolved = false;
    }

    public class WorkingCopy : ObservableObject
    {
        public bool IncludeUntracked
        {
            get => _repo.IncludeUntracked;
            set
            {
                if (_repo.IncludeUntracked != value)
                {
                    _repo.IncludeUntracked = value;
                    OnPropertyChanged(nameof(IncludeUntracked));
                }
            }
        }

        public bool CanCommitWithPush
        {
            get => _canCommitWithPush;
            set
            {
                if (SetProperty(ref _canCommitWithPush, value))
                    OnPropertyChanged(nameof(IsCommitWithPushVisible));
            }
        }

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
            set
            {
                if (SetProperty(ref _useAmend, value) && value)
                {
                    var currentBranch = _repo.Branches.Find(x => x.IsCurrent);
                    if (currentBranch == null)
                    {
                        App.RaiseException(_repo.FullPath, "No commits to amend!!!");
                        _useAmend = false;
                        OnPropertyChanged();
                        return;
                    }

                    CommitMessage = new Commands.QueryCommitFullMessage(_repo.FullPath, currentBranch.Head).Result();
                }

                OnPropertyChanged(nameof(IsCommitWithPushVisible));
            }
        }

        public bool IsCommitWithPushVisible
        {
            get => !UseAmend && CanCommitWithPush;
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

        public List<Models.Change> SelectedUnstaged
        {
            get => _selectedUnstaged;
            set
            {
                if (SetProperty(ref _selectedUnstaged, value))
                {
                    if (value == null || value.Count == 0)
                    {
                        if (_selectedStaged == null || _selectedStaged.Count == 0)
                            SetDetail(null);
                    }
                    else
                    {
                        SelectedStaged = [];

                        if (value.Count == 1)
                            SetDetail(value[0]);
                        else
                            SetDetail(null);
                    }
                }
            }
        }

        public List<Models.Change> SelectedStaged
        {
            get => _selectedStaged;
            set
            {
                if (SetProperty(ref _selectedStaged, value))
                {
                    if (value == null || value.Count == 0)
                    {
                        if (_selectedUnstaged == null || _selectedUnstaged.Count == 0)
                            SetDetail(null);
                    }
                    else
                    {
                        SelectedUnstaged = [];

                        if (value.Count == 1)
                            SetDetail(value[0]);
                        else
                            SetDetail(null);
                    }
                }
            }
        }

        public int Count => _count;

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

            _selectedUnstaged.Clear();
            OnPropertyChanged(nameof(SelectedUnstaged));

            _selectedStaged.Clear();
            OnPropertyChanged(nameof(SelectedStaged));

            _unstaged.Clear();
            OnPropertyChanged(nameof(Unstaged));

            _staged.Clear();
            OnPropertyChanged(nameof(Staged));

            _detailContext = null;
            _commitMessage = string.Empty;
        }

        public bool SetData(List<Models.Change> changes)
        {
            var unstaged = new List<Models.Change>();
            var staged = new List<Models.Change>();
            var selectedUnstaged = new List<Models.Change>();
            var selectedStaged = new List<Models.Change>();

            var lastSelectedUnstaged = new HashSet<string>();
            var lastSelectedStaged = new HashSet<string>();
            if (_selectedUnstaged != null)
            {
                foreach (var c in _selectedUnstaged)
                    lastSelectedUnstaged.Add(c.Path);
            }
            else if (_selectedStaged != null)
            {
                foreach (var c in _selectedStaged)
                    lastSelectedStaged.Add(c.Path);
            }

            var hasConflict = false;
            foreach (var c in changes)
            {
                if (c.Index == Models.ChangeState.Modified
                    || c.Index == Models.ChangeState.Added
                    || c.Index == Models.ChangeState.Deleted
                    || c.Index == Models.ChangeState.Renamed)
                {
                    staged.Add(c);

                    if (lastSelectedStaged.Contains(c.Path))
                        selectedStaged.Add(c);
                }

                if (c.WorkTree != Models.ChangeState.None)
                {
                    unstaged.Add(c);
                    hasConflict |= c.IsConflit;

                    if (lastSelectedUnstaged.Contains(c.Path))
                        selectedUnstaged.Add(c);
                }
            }

            _count = changes.Count;

            Dispatcher.UIThread.Invoke(() =>
            {
                _isLoadingData = true;
                Unstaged = unstaged;
                Staged = staged;
                _isLoadingData = false;

                if (selectedUnstaged.Count > 0)
                    SelectedUnstaged = selectedUnstaged;
                else if (selectedStaged.Count > 0)
                    SelectedStaged = selectedStaged;
                else
                    SetDetail(null);

                // Try to load merge message from MERGE_MSG
                if (string.IsNullOrEmpty(_commitMessage))
                {
                    var mergeMsgFile = Path.Combine(_repo.GitDir, "MERGE_MSG");
                    if (File.Exists(mergeMsgFile))
                        CommitMessage = File.ReadAllText(mergeMsgFile);
                }
            });

            return hasConflict;
        }

        public void OpenAssumeUnchanged()
        {
            var dialog = new Views.AssumeUnchangedManager()
            {
                DataContext = new AssumeUnchangedManager(_repo.FullPath)
            };

            dialog.ShowDialog(App.GetTopLevel() as Window);
        }

        public void StageSelected()
        {
            StageChanges(_selectedUnstaged);
        }

        public void StageAll()
        {
            StageChanges(_unstaged);
        }

        public async void StageChanges(List<Models.Change> changes)
        {
            if (_unstaged.Count == 0 || changes.Count == 0)
                return;

            SetDetail(null);
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

        public void UnstageSelected()
        {
            UnstageChanges(_selectedStaged);
        }

        public void UnstageAll()
        {
            UnstageChanges(_staged);
        }

        public async void UnstageChanges(List<Models.Change> changes)
        {
            if (_staged.Count == 0 || changes.Count == 0)
                return;

            SetDetail(null);
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
                        PopupHost.ShowPopup(new Discard(_repo));
                    else
                        PopupHost.ShowPopup(new Discard(_repo, changes, true));
                }
                else
                {
                    if (changes.Count == _staged.Count && _unstaged.Count == 0)
                        PopupHost.ShowPopup(new Discard(_repo));
                    else
                        PopupHost.ShowPopup(new Discard(_repo, changes, false));
                }
            }
        }

        public void Commit()
        {
            DoCommit(false);
        }

        public void CommitWithPush()
        {
            DoCommit(true);
        }

        public ContextMenu CreateContextMenuForUnstagedChanges()
        {
            if (_selectedUnstaged == null || _selectedUnstaged.Count == 0)
                return null;

            var menu = new ContextMenu();
            if (_selectedUnstaged.Count == 1)
            {
                var change = _selectedUnstaged[0];
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
                        UseTheirs(_selectedUnstaged);
                        e.Handled = true;
                    };

                    var useMine = new MenuItem();
                    useMine.Icon = App.CreateMenuIcon("Icons.Local");
                    useMine.Header = App.Text("FileCM.UseMine");
                    useMine.Click += (_, e) =>
                    {
                        UseMine(_selectedUnstaged);
                        e.Handled = true;
                    };

                    var openMerger = new MenuItem();
                    openMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
                    openMerger.Header = App.Text("FileCM.OpenWithExternalMerger");
                    openMerger.Click += (_, e) =>
                    {
                        UseExternalMergeTool(change);
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
                        StageChanges(_selectedUnstaged);
                        e.Handled = true;
                    };

                    var discard = new MenuItem();
                    discard.Header = App.Text("FileCM.Discard");
                    discard.Icon = App.CreateMenuIcon("Icons.Undo");
                    discard.Click += (_, e) =>
                    {
                        Discard(_selectedUnstaged, true);
                        e.Handled = true;
                    };

                    var stash = new MenuItem();
                    stash.Header = App.Text("FileCM.Stash");
                    stash.Icon = App.CreateMenuIcon("Icons.Stashes");
                    stash.Click += (_, e) =>
                    {
                        if (PopupHost.CanCreatePopup())
                        {
                            PopupHost.ShowPopup(new StashChanges(_repo, _selectedUnstaged, false));
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
                            var succ = await Task.Run(() => Commands.SaveChangesAsPatch.Exec(_repo.FullPath, _selectedUnstaged, true, storageFile.Path.LocalPath));
                            if (succ)
                                App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                        }

                        e.Handled = true;
                    };

                    var assumeUnchanged = new MenuItem();
                    assumeUnchanged.Header = App.Text("FileCM.AssumeUnchanged");
                    assumeUnchanged.Icon = App.CreateMenuIcon("Icons.File.Ignore");
                    assumeUnchanged.IsVisible = change.WorkTree != Models.ChangeState.Untracked;
                    assumeUnchanged.Click += (_, e) =>
                    {
                        new Commands.AssumeUnchanged(_repo.FullPath).Add(change.Path);
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

                    menu.Items.Add(stage);
                    menu.Items.Add(discard);
                    menu.Items.Add(stash);
                    menu.Items.Add(patch);
                    menu.Items.Add(assumeUnchanged);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(history);
                    menu.Items.Add(new MenuItem() { Header = "-" });

                    var extension = Path.GetExtension(change.Path);
                    var hasExtra = false;
                    if (change.WorkTree == Models.ChangeState.Untracked)
                    {
                        var isRooted = change.Path.IndexOf('/', StringComparison.Ordinal) <= 0;
                        var addToIgnore = new MenuItem();
                        addToIgnore.Header = App.Text("WorkingCopy.AddToGitIgnore");
                        addToIgnore.Icon = App.CreateMenuIcon("Icons.GitIgnore");

                        var singleFile = new MenuItem();
                        singleFile.Header = App.Text("WorkingCopy.AddToGitIgnore.SingleFile");
                        singleFile.Click += (_, e) =>
                        {
                            Commands.GitIgnore.Add(_repo.FullPath, change.Path);
                            e.Handled = true;
                        };
                        addToIgnore.Items.Add(singleFile);

                        var byParentFolder = new MenuItem();
                        byParentFolder.Header = App.Text("WorkingCopy.AddToGitIgnore.InSameFolder");
                        byParentFolder.IsVisible = !isRooted;
                        byParentFolder.Click += (_, e) =>
                        {
                            Commands.GitIgnore.Add(_repo.FullPath, Path.GetDirectoryName(change.Path) + "/");
                            e.Handled = true;
                        };
                        addToIgnore.Items.Add(byParentFolder);

                        if (!string.IsNullOrEmpty(extension))
                        {
                            var byExtension = new MenuItem();
                            byExtension.Header = App.Text("WorkingCopy.AddToGitIgnore.Extension", extension);
                            byExtension.Click += (_, e) =>
                            {
                                Commands.GitIgnore.Add(_repo.FullPath, "*" + extension);
                                e.Handled = true;
                            };
                            addToIgnore.Items.Add(byExtension);

                            var byExtensionInSameFolder = new MenuItem();
                            byExtensionInSameFolder.Header = App.Text("WorkingCopy.AddToGitIgnore.ExtensionInSameFolder", extension);
                            byExtensionInSameFolder.IsVisible = !isRooted;
                            byExtensionInSameFolder.Click += (_, e) =>
                            {
                                Commands.GitIgnore.Add(_repo.FullPath, Path.GetDirectoryName(change.Path) + "/*" + extension);
                                e.Handled = true;
                            };
                            addToIgnore.Items.Add(byExtensionInSameFolder);
                        }

                        menu.Items.Add(addToIgnore);
                        hasExtra = true;
                    }

                    var lfsEnabled = new Commands.LFS(_repo.FullPath).IsEnabled();
                    if (lfsEnabled)
                    {
                        var lfs = new MenuItem();
                        lfs.Header = App.Text("GitLFS");
                        lfs.Icon = App.CreateMenuIcon("Icons.LFS");

                        var isLFSFiltered = new Commands.IsLFSFiltered(_repo.FullPath, change.Path).Result();
                        if (!isLFSFiltered)
                        {
                            var filename = Path.GetFileName(change.Path);
                            var lfsTrackThisFile = new MenuItem();
                            lfsTrackThisFile.Header = App.Text("GitLFS.Track", filename);
                            lfsTrackThisFile.Click += async (_, e) =>
                            {
                                var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Track(filename, true));
                                if (succ)
                                    App.SendNotification(_repo.FullPath, $"Tracking file named {filename} successfully!");

                                e.Handled = true;
                            };
                            lfs.Items.Add(lfsTrackThisFile);

                            if (!string.IsNullOrEmpty(extension))
                            {
                                var lfsTrackByExtension = new MenuItem();
                                lfsTrackByExtension.Header = App.Text("GitLFS.TrackByExtension", extension);
                                lfsTrackByExtension.Click += async (_, e) =>
                                {
                                    var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Track("*" + extension, false));
                                    if (succ)
                                        App.SendNotification(_repo.FullPath, $"Tracking all *{extension} files successfully!");

                                    e.Handled = true;
                                };
                                lfs.Items.Add(lfsTrackByExtension);
                            }

                            lfs.Items.Add(new MenuItem() { Header = "-" });
                        }                        

                        var lfsLock = new MenuItem();
                        lfsLock.Header = App.Text("GitLFS.Locks.Lock");
                        lfsLock.Icon = App.CreateMenuIcon("Icons.Lock");
                        lfsLock.IsEnabled = _repo.Remotes.Count > 0;
                        if (_repo.Remotes.Count == 1)
                        {
                            lfsLock.Click += async (o, e) =>
                            {
                                var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Lock(_repo.Remotes[0].Name, change.Path));
                                if (succ)
                                    App.SendNotification(_repo.FullPath, $"Lock file \"{change.Path}\" successfully!");

                                e.Handled = true;
                            };
                        }
                        else
                        {
                            foreach (var remote in _repo.Remotes)
                            {
                                var remoteName = remote.Name;
                                var lockRemote = new MenuItem();
                                lockRemote.Header = remoteName;
                                lockRemote.Click += async (o, e) =>
                                {
                                    var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Lock(remoteName, change.Path));
                                    if (succ)
                                        App.SendNotification(_repo.FullPath, $"Lock file \"{change.Path}\" successfully!");

                                    e.Handled = true;
                                };
                                lfsLock.Items.Add(lockRemote);
                            }
                        }                        
                        lfs.Items.Add(lfsLock);

                        var lfsUnlock = new MenuItem();
                        lfsUnlock.Header = App.Text("GitLFS.Locks.Unlock");
                        lfsUnlock.Icon = App.CreateMenuIcon("Icons.Unlock");
                        lfsUnlock.IsEnabled = _repo.Remotes.Count > 0;
                        if (_repo.Remotes.Count == 1)
                        {
                            lfsUnlock.Click += async (o, e) =>
                            {
                                var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Unlock(_repo.Remotes[0].Name, change.Path, false));
                                if (succ)
                                    App.SendNotification(_repo.FullPath, $"Unlock file \"{change.Path}\" successfully!");

                                e.Handled = true;
                            };
                        }
                        else
                        {
                            foreach (var remote in _repo.Remotes)
                            {
                                var remoteName = remote.Name;
                                var unlockRemote = new MenuItem();
                                unlockRemote.Header = remoteName;
                                unlockRemote.Click += async (o, e) =>
                                {
                                    var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Unlock(remoteName, change.Path, false));
                                    if (succ)
                                        App.SendNotification(_repo.FullPath, $"Unlock file \"{change.Path}\" successfully!");

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

                var copy = new MenuItem();
                copy.Header = App.Text("CopyPath");
                copy.Icon = App.CreateMenuIcon("Icons.Copy");
                copy.Click += (_, e) =>
                {
                    App.CopyText(change.Path);
                    e.Handled = true;
                };
                menu.Items.Add(copy);

                var copyFileName = new MenuItem();
                copyFileName.Header = App.Text("CopyFileName");
                copyFileName.Icon = App.CreateMenuIcon("Icons.Copy");
                copyFileName.Click += (_, e) =>
                {
                    App.CopyText(Path.GetFileName(change.Path));
                    e.Handled = true;
                };
                menu.Items.Add(copyFileName);
            }
            else
            {
                var hasConflicts = false;
                var hasNoneConflicts = false;
                foreach (var change in _selectedUnstaged)
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
                        UseTheirs(_selectedUnstaged);
                        e.Handled = true;
                    };

                    var useMine = new MenuItem();
                    useMine.Icon = App.CreateMenuIcon("Icons.Local");
                    useMine.Header = App.Text("FileCM.UseMine");
                    useMine.Click += (_, e) =>
                    {
                        UseMine(_selectedUnstaged);
                        e.Handled = true;
                    };

                    menu.Items.Add(useTheirs);
                    menu.Items.Add(useMine);
                    return menu;
                }

                var stage = new MenuItem();
                stage.Header = App.Text("FileCM.StageMulti", _selectedUnstaged.Count);
                stage.Icon = App.CreateMenuIcon("Icons.File.Add");
                stage.Click += (_, e) =>
                {
                    StageChanges(_selectedUnstaged);
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Text("FileCM.DiscardMulti", _selectedUnstaged.Count);
                discard.Icon = App.CreateMenuIcon("Icons.Undo");
                discard.Click += (_, e) =>
                {
                    Discard(_selectedUnstaged, true);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.StashMulti", _selectedUnstaged.Count);
                stash.Icon = App.CreateMenuIcon("Icons.Stashes");
                stash.Click += (_, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new StashChanges(_repo, _selectedUnstaged, false));

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
                        var succ = await Task.Run(() => Commands.SaveChangesAsPatch.Exec(_repo.FullPath, _selectedUnstaged, true, storageFile.Path.LocalPath));
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

        public ContextMenu CreateContextMenuForStagedChanges()
        {
            if (_selectedStaged == null || _selectedStaged.Count == 0)
                return null;

            var menu = new ContextMenu();
            if (_selectedStaged.Count == 1)
            {
                var change = _selectedStaged[0];
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
                    UnstageChanges(_selectedStaged);
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Text("FileCM.Discard");
                discard.Icon = App.CreateMenuIcon("Icons.Undo");
                discard.Click += (_, e) =>
                {
                    Discard(_selectedStaged, false);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.Stash");
                stash.Icon = App.CreateMenuIcon("Icons.Stashes");
                stash.Click += (_, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new StashChanges(_repo, _selectedStaged, false));

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
                        var succ = await Task.Run(() => Commands.SaveChangesAsPatch.Exec(_repo.FullPath, _selectedStaged, false, storageFile.Path.LocalPath));
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

                var copyFileName = new MenuItem();
                copyFileName.Header = App.Text("CopyFileName");
                copyFileName.Icon = App.CreateMenuIcon("Icons.Copy");
                copyFileName.Click += (_, e) =>
                {
                    App.CopyText(Path.GetFileName(change.Path));
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

                var lfsEnabled = new Commands.LFS(_repo.FullPath).IsEnabled();
                if (lfsEnabled)
                {
                    var lfs = new MenuItem();
                    lfs.Header = App.Text("GitLFS");
                    lfs.Icon = App.CreateMenuIcon("Icons.LFS");

                    var lfsLock = new MenuItem();
                    lfsLock.Header = App.Text("GitLFS.Locks.Lock");
                    lfsLock.Icon = App.CreateMenuIcon("Icons.Lock");
                    lfsLock.IsEnabled = _repo.Remotes.Count > 0;
                    if (_repo.Remotes.Count == 1)
                    {
                        lfsLock.Click += async (o, e) =>
                        {
                            var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Lock(_repo.Remotes[0].Name, change.Path));
                            if (succ)
                                App.SendNotification(_repo.FullPath, $"Lock file \"{change.Path}\" successfully!");

                            e.Handled = true;
                        };
                    }
                    else
                    {
                        foreach (var remote in _repo.Remotes)
                        {
                            var remoteName = remote.Name;
                            var lockRemote = new MenuItem();
                            lockRemote.Header = remoteName;
                            lockRemote.Click += async (o, e) =>
                            {
                                var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Lock(remoteName, change.Path));
                                if (succ)
                                    App.SendNotification(_repo.FullPath, $"Lock file \"{change.Path}\" successfully!");

                                e.Handled = true;
                            };
                            lfsLock.Items.Add(lockRemote);
                        }
                    }                    
                    lfs.Items.Add(lfsLock);

                    var lfsUnlock = new MenuItem();
                    lfsUnlock.Header = App.Text("GitLFS.Locks.Unlock");
                    lfsUnlock.Icon = App.CreateMenuIcon("Icons.Unlock");
                    lfsUnlock.IsEnabled = _repo.Remotes.Count > 0;
                    if (_repo.Remotes.Count == 1)
                    {
                        lfsUnlock.Click += async (o, e) =>
                        {
                            var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Unlock(_repo.Remotes[0].Name, change.Path, false));
                            if (succ)
                                App.SendNotification(_repo.FullPath, $"Unlock file \"{change.Path}\" successfully!");

                            e.Handled = true;
                        };
                    }
                    else
                    {
                        foreach (var remote in _repo.Remotes)
                        {
                            var remoteName = remote.Name;
                            var unlockRemote = new MenuItem();
                            unlockRemote.Header = remoteName;
                            unlockRemote.Click += async (o, e) =>
                            {
                                var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Unlock(remoteName, change.Path, false));
                                if (succ)
                                    App.SendNotification(_repo.FullPath, $"Unlock file \"{change.Path}\" successfully!");

                                e.Handled = true;
                            };
                            lfsUnlock.Items.Add(unlockRemote);
                        }
                    }                    
                    lfs.Items.Add(lfsUnlock);

                    menu.Items.Add(lfs);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                menu.Items.Add(copyPath);
                menu.Items.Add(copyFileName);
            }
            else
            {
                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.UnstageMulti", _selectedStaged.Count);
                unstage.Icon = App.CreateMenuIcon("Icons.File.Remove");
                unstage.Click += (o, e) =>
                {
                    UnstageChanges(_selectedStaged);
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Text("FileCM.DiscardMulti", _selectedStaged.Count);
                discard.Icon = App.CreateMenuIcon("Icons.Undo");
                discard.Click += (_, e) =>
                {
                    Discard(_selectedStaged, false);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.StashMulti", _selectedStaged.Count);
                stash.Icon = App.CreateMenuIcon("Icons.Stashes");
                stash.Click += (_, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                    {
                        PopupHost.ShowPopup(new StashChanges(_repo, _selectedStaged, false));
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
                        var succ = await Task.Run(() => Commands.SaveChangesAsPatch.Exec(_repo.FullPath, _selectedStaged, false, storageFile.Path.LocalPath));
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
            if (_repo.Settings.CommitMessages.Count == 0)
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

            foreach (var message in _repo.Settings.CommitMessages)
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

        private void SetDetail(Models.Change change)
        {
            if (_isLoadingData)
                return;

            var isUnstaged = _selectedUnstaged != null && _selectedUnstaged.Count > 0;
            if (change == null)
                DetailContext = null;
            else if (change.IsConflit && isUnstaged)
                DetailContext = new ConflictContext(_repo.FullPath, change);
            else
                DetailContext = new DiffContext(_repo.FullPath, new Models.DiffOption(change, isUnstaged), _detailContext as DiffContext);
        }

        private async void UseTheirs(List<Models.Change> changes)
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

        private async void UseMine(List<Models.Change> changes)
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

        private async void UseExternalMergeTool(Models.Change change)
        {
            var toolType = Preference.Instance.ExternalMergeToolType;
            var toolPath = Preference.Instance.ExternalMergeToolPath;

            _repo.SetWatcherEnabled(false);
            await Task.Run(() => Commands.MergeTool.OpenForMerge(_repo.FullPath, toolType, toolPath, change.Path));
            _repo.SetWatcherEnabled(true);
        }

        private void DoCommit(bool autoPush)
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

            _repo.Settings.PushCommitMessage(_commitMessage);

            SetDetail(null);
            IsCommitting = true;
            _repo.SetWatcherEnabled(false);

            Task.Run(() =>
            {
                var succ = new Commands.Commit(_repo.FullPath, _commitMessage, _useAmend).Exec();
                Dispatcher.UIThread.Post(() =>
                {
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
                });
            });
        }

        private Repository _repo = null;
        private bool _isLoadingData = false;
        private bool _isStaging = false;
        private bool _isUnstaging = false;
        private bool _isCommitting = false;
        private bool _useAmend = false;
        private bool _canCommitWithPush = false;
        private List<Models.Change> _unstaged = [];
        private List<Models.Change> _staged = [];
        private List<Models.Change> _selectedUnstaged = [];
        private List<Models.Change> _selectedStaged = [];
        private int _count = 0;
        private object _detailContext = null;
        private string _commitMessage = string.Empty;
    }
}
