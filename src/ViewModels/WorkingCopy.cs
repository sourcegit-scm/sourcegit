using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public record CommitMessageRecord(string subject)
    {
        public string Subject { get; set; } = subject;
    }

    public class WorkingCopy : ObservableObject, IDisposable
    {
        public bool IncludeUntracked
        {
            get => _repo.IncludeUntracked;
            set
            {
                if (_repo.IncludeUntracked != value)
                {
                    _repo.IncludeUntracked = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasRemotes
        {
            get => _hasRemotes;
            set => SetProperty(ref _hasRemotes, value);
        }

        public bool HasUnsolvedConflicts
        {
            get => _hasUnsolvedConflicts;
            set => SetProperty(ref _hasUnsolvedConflicts, value);
        }

        public InProgressContext InProgressContext
        {
            get => _inProgressContext;
            private set => SetProperty(ref _inProgressContext, value);
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

        public bool EnableSignOff
        {
            get => _repo.Settings.EnableSignOffForCommit;
            set => _repo.Settings.EnableSignOffForCommit = value;
        }

        public bool UseAmend
        {
            get => _useAmend;
            set
            {
                if (SetProperty(ref _useAmend, value))
                {
                    if (value)
                    {
                        var currentBranch = _repo.CurrentBranch;
                        if (currentBranch == null)
                        {
                            App.RaiseException(_repo.FullPath, "No commits to amend!!!");
                            _useAmend = false;
                            OnPropertyChanged();
                            return;
                        }

                        CommitMessage = new Commands.QueryCommitFullMessage(_repo.FullPath, currentBranch.Head)
                            .GetResultAsync()
                            .Result;
                    }
                    else
                    {
                        CommitMessage = string.Empty;
                        ResetAuthor = false;
                    }

                    Staged = GetStagedChanges();
                    VisibleStaged = GetVisibleChanges(_staged);
                    SelectedStaged = [];
                }
            }
        }

        public bool ResetAuthor
        {
            get => _resetAuthor;
            set => SetProperty(ref _resetAuthor, value);
        }

        public string Filter
        {
            get => _filter;
            set
            {
                if (SetProperty(ref _filter, value))
                {
                    if (_isLoadingData)
                        return;

                    VisibleUnstaged = GetVisibleChanges(_unstaged);
                    VisibleStaged = GetVisibleChanges(_staged);
                    SelectedUnstaged = [];
                }
            }
        }

        public List<Models.Change> Unstaged
        {
            get => _unstaged;
            private set => SetProperty(ref _unstaged, value);
        }

        public List<Models.Change> VisibleUnstaged
        {
            get => _visibleUnstaged;
            private set => SetProperty(ref _visibleUnstaged, value);
        }

        public List<Models.Change> Staged
        {
            get => _staged;
            private set => SetProperty(ref _staged, value);
        }

        public List<Models.Change> VisibleStaged
        {
            get => _visibleStaged;
            private set => SetProperty(ref _visibleStaged, value);
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
                            SetDetail(null, true);
                    }
                    else
                    {
                        if (_selectedStaged is { Count: > 0 })
                            SelectedStaged = [];

                        if (value.Count == 1)
                            SetDetail(value[0], true);
                        else
                            SetDetail(null, true);
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
                            SetDetail(null, false);
                    }
                    else
                    {
                        if (_selectedUnstaged is { Count: > 0 })
                            SelectedUnstaged = [];

                        if (value.Count == 1)
                            SetDetail(value[0], false);
                        else
                            SetDetail(null, false);
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

        public void Dispose()
        {
            _repo = null;
            _inProgressContext = null;

            _selectedUnstaged.Clear();
            OnPropertyChanged(nameof(SelectedUnstaged));

            _selectedStaged.Clear();
            OnPropertyChanged(nameof(SelectedStaged));

            _visibleUnstaged.Clear();
            OnPropertyChanged(nameof(VisibleUnstaged));

            _visibleStaged.Clear();
            OnPropertyChanged(nameof(VisibleStaged));

            _unstaged.Clear();
            OnPropertyChanged(nameof(Unstaged));

            _staged.Clear();
            OnPropertyChanged(nameof(Staged));

            _detailContext = null;
            _commitMessage = string.Empty;
        }

        public void SetData(List<Models.Change> changes)
        {
            if (!IsChanged(_cached, changes))
            {
                // Just force refresh selected changes.
                Dispatcher.UIThread.Invoke(() =>
                {
                    HasUnsolvedConflicts = _cached.Find(x => x.IsConflicted) != null;

                    UpdateDetail();
                    UpdateInProgressState();
                });

                return;
            }

            _cached = changes;

            var lastSelectedUnstaged = new HashSet<string>();
            var lastSelectedStaged = new HashSet<string>();
            if (_selectedUnstaged is { Count: > 0 })
            {
                foreach (var c in _selectedUnstaged)
                    lastSelectedUnstaged.Add(c.Path);
            }
            else if (_selectedStaged is { Count: > 0 })
            {
                foreach (var c in _selectedStaged)
                    lastSelectedStaged.Add(c.Path);
            }

            var unstaged = new List<Models.Change>();
            var hasConflict = false;
            foreach (var c in changes)
            {
                if (c.WorkTree != Models.ChangeState.None)
                {
                    unstaged.Add(c);
                    hasConflict |= c.IsConflicted;
                }
            }

            var visibleUnstaged = GetVisibleChanges(unstaged);
            var selectedUnstaged = new List<Models.Change>();
            foreach (var c in visibleUnstaged)
            {
                if (lastSelectedUnstaged.Contains(c.Path))
                    selectedUnstaged.Add(c);
            }

            var staged = GetStagedChanges();

            var visibleStaged = GetVisibleChanges(staged);
            var selectedStaged = new List<Models.Change>();
            foreach (var c in visibleStaged)
            {
                if (lastSelectedStaged.Contains(c.Path))
                    selectedStaged.Add(c);
            }

            Dispatcher.UIThread.Invoke(() =>
            {
                _isLoadingData = true;
                HasUnsolvedConflicts = hasConflict;
                VisibleUnstaged = visibleUnstaged;
                VisibleStaged = visibleStaged;
                Unstaged = unstaged;
                Staged = staged;
                SelectedUnstaged = selectedUnstaged;
                SelectedStaged = selectedStaged;
                _isLoadingData = false;

                UpdateDetail();
                UpdateInProgressState();
            });
        }

        public void OpenWithDefaultEditor(Models.Change c)
        {
            var absPath = Native.OS.GetAbsPath(_repo.FullPath, c.Path);
            if (File.Exists(absPath))
                Native.OS.OpenWithDefaultEditor(absPath);
        }

        public void StashAll(bool autoStart)
        {
            if (!_repo.CanCreatePopup())
                return;

            if (autoStart)
                _repo.ShowAndStartPopup(new StashChanges(_repo, _cached, false));
            else
                _repo.ShowPopup(new StashChanges(_repo, _cached, false));
        }

        public void StageSelected(Models.Change next)
        {
            StageChanges(_selectedUnstaged, next);
        }

        public void StageAll()
        {
            StageChanges(_visibleUnstaged, null);
        }

        public void UnstageSelected(Models.Change next)
        {
            UnstageChanges(_selectedStaged, next);
        }

        public void UnstageAll()
        {
            UnstageChanges(_visibleStaged, null);
        }

        public void Discard(List<Models.Change> changes)
        {
            if (_repo.CanCreatePopup())
                _repo.ShowPopup(new Discard(_repo, changes));
        }

        public void ClearFilter()
        {
            Filter = string.Empty;
        }

        public async void UseTheirs(List<Models.Change> changes)
        {
            _repo.SetWatcherEnabled(false);

            var files = new List<string>();
            var needStage = new List<string>();
            var log = _repo.CreateLog("Use Theirs");

            foreach (var change in changes)
            {
                if (!change.IsConflicted)
                    continue;

                if (change.ConflictReason is Models.ConflictReason.BothDeleted or Models.ConflictReason.DeletedByThem or Models.ConflictReason.AddedByUs)
                {
                    var fullpath = Path.Combine(_repo.FullPath, change.Path);
                    if (File.Exists(fullpath))
                        File.Delete(fullpath);

                    needStage.Add(change.Path);
                }
                else
                {
                    files.Add(change.Path);
                }
            }

            if (files.Count > 0)
            {
                var succ = await new Commands.Checkout(_repo.FullPath).Use(log).UseTheirsAsync(files);
                if (succ)
                    needStage.AddRange(files);
            }

            if (needStage.Count > 0)
            {
                var pathSpecFile = Path.GetTempFileName();
                await File.WriteAllLinesAsync(pathSpecFile, needStage);
                await new Commands.Add(_repo.FullPath, pathSpecFile).Use(log).ExecAsync();
                File.Delete(pathSpecFile);
            }

            log.Complete();
            _repo.MarkWorkingCopyDirtyManually();
            _repo.SetWatcherEnabled(true);
        }

        public async void UseMine(List<Models.Change> changes)
        {
            _repo.SetWatcherEnabled(false);

            var files = new List<string>();
            var needStage = new List<string>();
            var log = _repo.CreateLog("Use Mine");

            foreach (var change in changes)
            {
                if (!change.IsConflicted)
                    continue;

                if (change.ConflictReason is Models.ConflictReason.BothDeleted or Models.ConflictReason.DeletedByUs or Models.ConflictReason.AddedByThem)
                {
                    var fullpath = Path.Combine(_repo.FullPath, change.Path);
                    if (File.Exists(fullpath))
                        File.Delete(fullpath);

                    needStage.Add(change.Path);
                }
                else
                {
                    files.Add(change.Path);
                }
            }

            if (files.Count > 0)
            {
                var succ = await new Commands.Checkout(_repo.FullPath).Use(log).UseMineAsync(files);
                if (succ)
                    needStage.AddRange(files);
            }

            if (needStage.Count > 0)
            {
                var pathSpecFile = Path.GetTempFileName();
                await File.WriteAllLinesAsync(pathSpecFile, needStage);
                await new Commands.Add(_repo.FullPath, pathSpecFile).Use(log).ExecAsync();
                File.Delete(pathSpecFile);
            }

            log.Complete();
            _repo.MarkWorkingCopyDirtyManually();
            _repo.SetWatcherEnabled(true);
        }

        public async Task<bool> UseExternalMergeTool(Models.Change change)
        {
            var toolType = Preferences.Instance.ExternalMergeToolType;
            var toolPath = Preferences.Instance.ExternalMergeToolPath;
            var file = change?.Path;
            return await new Commands.MergeTool(_repo.FullPath, toolType, toolPath, file).OpenAsync();
        }

        public void ContinueMerge()
        {
            IsCommitting = true;

            if (_inProgressContext != null)
            {
                _repo.SetWatcherEnabled(false);
                Task.Run(async () =>
                {
                    var mergeMsgFile = Path.Combine(_repo.GitDir, "MERGE_MSG");
                    if (File.Exists(mergeMsgFile) && !string.IsNullOrWhiteSpace(_commitMessage))
                        await File.WriteAllTextAsync(mergeMsgFile, _commitMessage);

                    var succ = await _inProgressContext.ContinueAsync();
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (succ)
                            CommitMessage = string.Empty;

                        _repo.SetWatcherEnabled(true);
                        IsCommitting = false;
                    });
                });
            }
            else
            {
                _repo.MarkWorkingCopyDirtyManually();
                IsCommitting = false;
            }
        }

        public void SkipMerge()
        {
            IsCommitting = true;

            if (_inProgressContext != null)
            {
                _repo.SetWatcherEnabled(false);
                Task.Run(async () =>
                {
                    var succ = await _inProgressContext.SkipAsync();
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (succ)
                            CommitMessage = string.Empty;

                        _repo.SetWatcherEnabled(true);
                        IsCommitting = false;
                    });
                });
            }
            else
            {
                _repo.MarkWorkingCopyDirtyManually();
                IsCommitting = false;
            }
        }

        public void AbortMerge()
        {
            IsCommitting = true;

            if (_inProgressContext != null)
            {
                _repo.SetWatcherEnabled(false);
                Task.Run(async () =>
                {
                    var succ = await _inProgressContext.AbortAsync();
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (succ)
                            CommitMessage = string.Empty;

                        _repo.SetWatcherEnabled(true);
                        IsCommitting = false;
                    });
                });
            }
            else
            {
                _repo.MarkWorkingCopyDirtyManually();
                IsCommitting = false;
            }
        }

        public void Commit()
        {
            DoCommit(false, false);
        }

        public void CommitWithAutoStage()
        {
            DoCommit(true, false);
        }

        public void CommitWithPush()
        {
            DoCommit(false, true);
        }

        public ContextMenu CreateContextMenuForUnstagedChanges(string selectedSingleFolder)
        {
            if (_selectedUnstaged == null || _selectedUnstaged.Count == 0)
                return null;

            var hasSelectedFolder = !string.IsNullOrEmpty(selectedSingleFolder);
            var menu = new ContextMenu();
            if (_selectedUnstaged.Count == 1)
            {
                var change = _selectedUnstaged[0];
                var path = Native.OS.GetAbsPath(_repo.FullPath, change.Path);

                if (!change.IsConflicted || change.ConflictReason is Models.ConflictReason.BothAdded or Models.ConflictReason.BothModified)
                {
                    var openMerger = new MenuItem();
                    openMerger.Header = App.Text("OpenInExternalMergeTool");
                    openMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
                    openMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
                    openMerger.Click += async (_, e) =>
                    {
                        if (change.IsConflicted)
                        {
                            await UseExternalMergeTool(change);
                        }
                        else
                        {
                            var toolType = Preferences.Instance.ExternalMergeToolType;
                            var toolPath = Preferences.Instance.ExternalMergeToolPath;
                            var opt = new Models.DiffOption(change, true);
                            new Commands.DiffTool(_repo.FullPath, toolType, toolPath, opt).Open();
                        }

                        e.Handled = true;
                    };
                    menu.Items.Add(openMerger);
                }

                var openWith = new MenuItem();
                openWith.Header = App.Text("OpenWith");
                openWith.Icon = App.CreateMenuIcon("Icons.OpenWith");
                openWith.Tag = OperatingSystem.IsMacOS() ? "⌘+O" : "Ctrl+O";
                openWith.IsEnabled = File.Exists(path);
                openWith.Click += (_, e) =>
                {
                    OpenWithDefaultEditor(change);
                    e.Handled = true;
                };
                menu.Items.Add(openWith);

                var explore = new MenuItem();
                explore.Header = App.Text("RevealFile");
                explore.Icon = App.CreateMenuIcon("Icons.Explore");
                explore.IsEnabled = File.Exists(path) || Directory.Exists(path);
                explore.Click += (_, e) =>
                {
                    var target = hasSelectedFolder ? Native.OS.GetAbsPath(_repo.FullPath, selectedSingleFolder) : path;
                    Native.OS.OpenInFileManager(target, true);
                    e.Handled = true;
                };
                menu.Items.Add(explore);
                menu.Items.Add(new MenuItem() { Header = "-" });

                if (change.IsConflicted)
                {
                    var useTheirs = new MenuItem();
                    useTheirs.Icon = App.CreateMenuIcon("Icons.Incoming");
                    useTheirs.Click += (_, e) =>
                    {
                        UseTheirs(_selectedUnstaged);
                        e.Handled = true;
                    };

                    var useMine = new MenuItem();
                    useMine.Icon = App.CreateMenuIcon("Icons.Local");
                    useMine.Click += (_, e) =>
                    {
                        UseMine(_selectedUnstaged);
                        e.Handled = true;
                    };

                    switch (_inProgressContext)
                    {
                        case CherryPickInProgress cherryPick:
                            useTheirs.Header = App.Text("FileCM.ResolveUsing", cherryPick.HeadName);
                            useMine.Header = App.Text("FileCM.ResolveUsing", _repo.CurrentBranch.Name);
                            break;
                        case RebaseInProgress rebase:
                            useTheirs.Header = App.Text("FileCM.ResolveUsing", rebase.HeadName);
                            useMine.Header = App.Text("FileCM.ResolveUsing", rebase.BaseName);
                            break;
                        case RevertInProgress revert:
                            useTheirs.Header = App.Text("FileCM.ResolveUsing", $"{revert.Head.SHA.AsSpan(0, 10)} (revert)");
                            useMine.Header = App.Text("FileCM.ResolveUsing", _repo.CurrentBranch.Name);
                            break;
                        case MergeInProgress merge:
                            useTheirs.Header = App.Text("FileCM.ResolveUsing", merge.SourceName);
                            useMine.Header = App.Text("FileCM.ResolveUsing", _repo.CurrentBranch.Name);
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
                    stage.Click += (_, e) =>
                    {
                        StageChanges(_selectedUnstaged, null);
                        e.Handled = true;
                    };

                    var discard = new MenuItem();
                    discard.Header = App.Text("FileCM.Discard");
                    discard.Icon = App.CreateMenuIcon("Icons.Undo");
                    discard.Tag = "Back/Delete";
                    discard.Click += (_, e) =>
                    {
                        Discard(_selectedUnstaged);
                        e.Handled = true;
                    };

                    var stash = new MenuItem();
                    stash.Header = App.Text("FileCM.Stash");
                    stash.Icon = App.CreateMenuIcon("Icons.Stashes.Add");
                    stash.Click += (_, e) =>
                    {
                        if (_repo.CanCreatePopup())
                            _repo.ShowPopup(new StashChanges(_repo, _selectedUnstaged, true));

                        e.Handled = true;
                    };

                    var patch = new MenuItem();
                    patch.Header = App.Text("FileCM.SaveAsPatch");
                    patch.Icon = App.CreateMenuIcon("Icons.Diff");
                    patch.Click += async (_, e) =>
                    {
                        var storageProvider = App.GetStorageProvider();
                        if (storageProvider == null)
                            return;

                        var options = new FilePickerSaveOptions();
                        options.Title = App.Text("FileCM.SaveAsPatch");
                        options.DefaultExtension = ".patch";
                        options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                        var storageFile = await storageProvider.SaveFilePickerAsync(options);
                        if (storageFile != null)
                        {
                            var succ = await Commands.SaveChangesAsPatch.ProcessLocalChangesAsync(_repo.FullPath, _selectedUnstaged, true, storageFile.Path.LocalPath);
                            if (succ)
                                App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                        }

                        e.Handled = true;
                    };

                    var assumeUnchanged = new MenuItem();
                    assumeUnchanged.Header = App.Text("FileCM.AssumeUnchanged");
                    assumeUnchanged.Icon = App.CreateMenuIcon("Icons.File.Ignore");
                    assumeUnchanged.IsVisible = change.WorkTree != Models.ChangeState.Untracked;
                    assumeUnchanged.Click += async (_, e) =>
                    {
                        var log = _repo.CreateLog("Assume File Unchanged");
                        await new Commands.AssumeUnchanged(_repo.FullPath, change.Path, true).Use(log).ExecAsync();
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
                                if (_repo.CanCreatePopup())
                                    _repo.ShowPopup(new AddToIgnore(_repo, $"{selectedSingleFolder}/"));
                                e.Handled = true;
                            };
                            addToIgnore.Items.Add(ignoreFolder);
                        }
                        else
                        {
                            var isRooted = change.Path.IndexOf('/') <= 0;
                            var singleFile = new MenuItem();
                            singleFile.Header = App.Text("WorkingCopy.AddToGitIgnore.SingleFile");
                            singleFile.Click += (_, e) =>
                            {
                                if (_repo.CanCreatePopup())
                                    _repo.ShowPopup(new AddToIgnore(_repo, change.Path));
                                e.Handled = true;
                            };
                            addToIgnore.Items.Add(singleFile);

                            if (!string.IsNullOrEmpty(extension))
                            {
                                var byExtension = new MenuItem();
                                byExtension.Header = App.Text("WorkingCopy.AddToGitIgnore.Extension", extension);
                                byExtension.Click += (_, e) =>
                                {
                                    if (_repo.CanCreatePopup())
                                        _repo.ShowPopup(new AddToIgnore(_repo, $"*{extension}"));
                                    e.Handled = true;
                                };
                                addToIgnore.Items.Add(byExtension);

                                var byExtensionInSameFolder = new MenuItem();
                                byExtensionInSameFolder.Header = App.Text("WorkingCopy.AddToGitIgnore.ExtensionInSameFolder", extension);
                                byExtensionInSameFolder.IsVisible = !isRooted;
                                byExtensionInSameFolder.Click += (_, e) =>
                                {
                                    var dir = Path.GetDirectoryName(change.Path)!.Replace('\\', '/').TrimEnd('/');
                                    if (_repo.CanCreatePopup())
                                        _repo.ShowPopup(new AddToIgnore(_repo, $"{dir}/*{extension}"));
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
                            if (_repo.CanCreatePopup())
                                _repo.ShowPopup(new AddToIgnore(_repo, $"{selectedSingleFolder}/"));
                            e.Handled = true;
                        };
                        addToIgnore.Items.Add(ignoreFolder);

                        menu.Items.Add(addToIgnore);
                        hasExtra = true;
                    }

                    if (_repo.IsLFSEnabled())
                    {
                        var lfs = new MenuItem();
                        lfs.Header = App.Text("GitLFS");
                        lfs.Icon = App.CreateMenuIcon("Icons.LFS");

                        var isLFSFiltered = new Commands.IsLFSFiltered(_repo.FullPath, change.Path).GetResultAsync().Result;
                        if (!isLFSFiltered)
                        {
                            var filename = Path.GetFileName(change.Path);
                            var lfsTrackThisFile = new MenuItem();
                            lfsTrackThisFile.Header = App.Text("GitLFS.Track", filename);
                            lfsTrackThisFile.Click += async (_, e) =>
                            {
                                await _repo.TrackLFSFileAsync(filename, true);
                                e.Handled = true;
                            };
                            lfs.Items.Add(lfsTrackThisFile);

                            if (!string.IsNullOrEmpty(extension))
                            {
                                var lfsTrackByExtension = new MenuItem();
                                lfsTrackByExtension.Header = App.Text("GitLFS.TrackByExtension", extension);
                                lfsTrackByExtension.Click += async (_, e) =>
                                {
                                    await _repo.TrackLFSFileAsync($"*{extension}", false);
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
                            lfsLock.Click += async (_, e) =>
                            {
                                await _repo.LockLFSFileAsync(_repo.Remotes[0].Name, change.Path);
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
                                lockRemote.Click += async (_, e) =>
                                {
                                    await _repo.LockLFSFileAsync(remoteName, change.Path);
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
                            lfsUnlock.Click += async (_, e) =>
                            {
                                await _repo.UnlockLFSFileAsync(_repo.Remotes[0].Name, change.Path, false, true);
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
                                unlockRemote.Click += async (_, e) =>
                                {
                                    await _repo.UnlockLFSFileAsync(remoteName, change.Path, false, true);
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
                        App.ShowWindow(new DirHistories(_repo, selectedSingleFolder));
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
                        App.ShowWindow(new FileHistories(_repo, change.Path));
                        e.Handled = true;
                    };

                    var blame = new MenuItem();
                    blame.Header = App.Text("Blame") + " (HEAD-only)";
                    blame.Icon = App.CreateMenuIcon("Icons.Blame");
                    blame.Click += async (_, ev) =>
                    {
                        var commit = await new Commands.QuerySingleCommit(_repo.FullPath, "HEAD").GetResultAsync();
                        App.ShowWindow(new Blame(_repo.FullPath, change.Path, commit));
                        ev.Handled = true;
                    };

                    menu.Items.Add(history);
                    menu.Items.Add(blame);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

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
                    await App.CopyTextAsync(hasSelectedFolder ? Native.OS.GetAbsPath(_repo.FullPath, selectedSingleFolder) : path);
                    e.Handled = true;
                };

                menu.Items.Add(copy);
                menu.Items.Add(copyFullPath);
            }
            else
            {
                var hasConflicts = false;
                var hasNonConflicts = false;
                foreach (var change in _selectedUnstaged)
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
                        App.RaiseException(_repo.FullPath, "Selection contains both conflict and non-conflict changes!");
                        return null;
                    }

                    var useTheirs = new MenuItem();
                    useTheirs.Icon = App.CreateMenuIcon("Icons.Incoming");
                    useTheirs.Click += (_, e) =>
                    {
                        UseTheirs(_selectedUnstaged);
                        e.Handled = true;
                    };

                    var useMine = new MenuItem();
                    useMine.Icon = App.CreateMenuIcon("Icons.Local");
                    useMine.Click += (_, e) =>
                    {
                        UseMine(_selectedUnstaged);
                        e.Handled = true;
                    };

                    switch (_inProgressContext)
                    {
                        case CherryPickInProgress cherryPick:
                            useTheirs.Header = App.Text("FileCM.ResolveUsing", cherryPick.HeadName);
                            useMine.Header = App.Text("FileCM.ResolveUsing", _repo.CurrentBranch.Name);
                            break;
                        case RebaseInProgress rebase:
                            useTheirs.Header = App.Text("FileCM.ResolveUsing", rebase.HeadName);
                            useMine.Header = App.Text("FileCM.ResolveUsing", rebase.BaseName);
                            break;
                        case RevertInProgress revert:
                            useTheirs.Header = App.Text("FileCM.ResolveUsing", $"{revert.Head.SHA.AsSpan(0, 10)} (revert)");
                            useMine.Header = App.Text("FileCM.ResolveUsing", _repo.CurrentBranch.Name);
                            break;
                        case MergeInProgress merge:
                            useTheirs.Header = App.Text("FileCM.ResolveUsing", merge.SourceName);
                            useMine.Header = App.Text("FileCM.ResolveUsing", _repo.CurrentBranch.Name);
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
                    var dir = Path.Combine(_repo.FullPath, selectedSingleFolder);
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
                stage.Header = App.Text("FileCM.StageMulti", _selectedUnstaged.Count);
                stage.Icon = App.CreateMenuIcon("Icons.File.Add");
                stage.Tag = "Enter/Space";
                stage.Click += (_, e) =>
                {
                    StageChanges(_selectedUnstaged, null);
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Text("FileCM.DiscardMulti", _selectedUnstaged.Count);
                discard.Icon = App.CreateMenuIcon("Icons.Undo");
                discard.Tag = "Back/Delete";
                discard.Click += (_, e) =>
                {
                    Discard(_selectedUnstaged);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.StashMulti", _selectedUnstaged.Count);
                stash.Icon = App.CreateMenuIcon("Icons.Stashes.Add");
                stash.Click += (_, e) =>
                {
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new StashChanges(_repo, _selectedUnstaged, true));

                    e.Handled = true;
                };

                var patch = new MenuItem();
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Icon = App.CreateMenuIcon("Icons.Diff");
                patch.Click += async (_, e) =>
                {
                    var storageProvider = App.GetStorageProvider();
                    if (storageProvider == null)
                        return;

                    var options = new FilePickerSaveOptions();
                    options.Title = App.Text("FileCM.SaveAsPatch");
                    options.DefaultExtension = ".patch";
                    options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                    var storageFile = await storageProvider.SaveFilePickerAsync(options);
                    if (storageFile != null)
                    {
                        var succ = await Commands.SaveChangesAsPatch.ProcessLocalChangesAsync(_repo.FullPath, _selectedUnstaged, true, storageFile.Path.LocalPath);
                        if (succ)
                            App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
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
                        if (_repo.CanCreatePopup())
                            _repo.ShowPopup(new AddToIgnore(_repo, $"{selectedSingleFolder}/"));
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
                        App.ShowWindow(new DirHistories(_repo, selectedSingleFolder));
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
                        await App.CopyTextAsync(Native.OS.GetAbsPath(_repo.FullPath, selectedSingleFolder));
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

        public ContextMenu CreateContextMenuForStagedChanges(string selectedSingleFolder)
        {
            if (_selectedStaged == null || _selectedStaged.Count == 0)
                return null;

            var menu = new ContextMenu();

            MenuItem ai = null;
            var services = _repo.GetPreferredOpenAIServices();
            if (services.Count > 0)
            {
                ai = new MenuItem();
                ai.Icon = App.CreateMenuIcon("Icons.AIAssist");
                ai.Header = App.Text("ChangeCM.GenerateCommitMessage");

                if (services.Count == 1)
                {
                    ai.Click += async (_, e) =>
                    {
                        await App.ShowDialog(new AIAssistant(_repo, services[0], _selectedStaged, t => CommitMessage = t));
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
                            await App.ShowDialog(new AIAssistant(_repo, dup, _selectedStaged, t => CommitMessage = t));
                            e.Handled = true;
                        };

                        ai.Items.Add(item);
                    }
                }
            }

            var hasSelectedFolder = !string.IsNullOrEmpty(selectedSingleFolder);
            if (_selectedStaged.Count == 1)
            {
                var change = _selectedStaged[0];
                var path = Native.OS.GetAbsPath(_repo.FullPath, change.Path);

                var openWithMerger = new MenuItem();
                openWithMerger.Header = App.Text("OpenInExternalMergeTool");
                openWithMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
                openWithMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
                openWithMerger.Click += (_, ev) =>
                {
                    var toolType = Preferences.Instance.ExternalMergeToolType;
                    var toolPath = Preferences.Instance.ExternalMergeToolPath;
                    var opt = new Models.DiffOption(change, false);
                    new Commands.DiffTool(_repo.FullPath, toolType, toolPath, opt).Open();
                    ev.Handled = true;
                };

                var openWith = new MenuItem();
                openWith.Header = App.Text("OpenWith");
                openWith.Icon = App.CreateMenuIcon("Icons.OpenWith");
                openWith.Tag = OperatingSystem.IsMacOS() ? "⌘+O" : "Ctrl+O";
                openWith.IsEnabled = File.Exists(path);
                openWith.Click += (_, e) =>
                {
                    OpenWithDefaultEditor(change);
                    e.Handled = true;
                };

                var explore = new MenuItem();
                explore.IsEnabled = File.Exists(path) || Directory.Exists(path);
                explore.Header = App.Text("RevealFile");
                explore.Icon = App.CreateMenuIcon("Icons.Explore");
                explore.Click += (_, e) =>
                {
                    var target = hasSelectedFolder ? Native.OS.GetAbsPath(_repo.FullPath, selectedSingleFolder) : path;
                    Native.OS.OpenInFileManager(target, true);
                    e.Handled = true;
                };

                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.Unstage");
                unstage.Icon = App.CreateMenuIcon("Icons.File.Remove");
                unstage.Tag = "Enter/Space";
                unstage.Click += (_, e) =>
                {
                    UnstageChanges(_selectedStaged, null);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.Stash");
                stash.Icon = App.CreateMenuIcon("Icons.Stashes.Add");
                stash.Click += (_, e) =>
                {
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new StashChanges(_repo, _selectedStaged, true));

                    e.Handled = true;
                };

                var patch = new MenuItem();
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Icon = App.CreateMenuIcon("Icons.Diff");
                patch.Click += async (_, e) =>
                {
                    var storageProvider = App.GetStorageProvider();
                    if (storageProvider == null)
                        return;

                    var options = new FilePickerSaveOptions();
                    options.Title = App.Text("FileCM.SaveAsPatch");
                    options.DefaultExtension = ".patch";
                    options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                    var storageFile = await storageProvider.SaveFilePickerAsync(options);
                    if (storageFile != null)
                    {
                        var succ = await Commands.SaveChangesAsPatch.ProcessLocalChangesAsync(_repo.FullPath, _selectedStaged, false, storageFile.Path.LocalPath);
                        if (succ)
                            App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                    }

                    e.Handled = true;
                };

                menu.Items.Add(openWithMerger);
                menu.Items.Add(openWith);
                menu.Items.Add(explore);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(unstage);
                menu.Items.Add(stash);
                menu.Items.Add(patch);
                menu.Items.Add(new MenuItem() { Header = "-" });

                if (_repo.IsLFSEnabled())
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
                        lfsLock.Click += async (_, e) =>
                        {
                            await _repo.LockLFSFileAsync(_repo.Remotes[0].Name, change.Path);
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
                            lockRemote.Click += async (_, e) =>
                            {
                                await _repo.LockLFSFileAsync(remoteName, change.Path);
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
                        lfsUnlock.Click += async (_, e) =>
                        {
                            await _repo.UnlockLFSFileAsync(_repo.Remotes[0].Name, change.Path, false, true);
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
                            unlockRemote.Click += async (_, e) =>
                            {
                                await _repo.UnlockLFSFileAsync(remoteName, change.Path, false, true);
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
                        App.ShowWindow(new DirHistories(_repo, selectedSingleFolder));
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
                        App.ShowWindow(new FileHistories(_repo, change.Path));
                        e.Handled = true;
                    };

                    var blame = new MenuItem();
                    blame.Header = App.Text("Blame") + " (HEAD-only)";
                    blame.Icon = App.CreateMenuIcon("Icons.Blame");
                    blame.Click += async (_, e) =>
                    {
                        var commit = await new Commands.QuerySingleCommit(_repo.FullPath, "HEAD").GetResultAsync();
                        App.ShowWindow(new Blame(_repo.FullPath, change.Path, commit));
                        e.Handled = true;
                    };

                    menu.Items.Add(history);
                    menu.Items.Add(blame);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

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
                    var target = hasSelectedFolder ? Native.OS.GetAbsPath(_repo.FullPath, selectedSingleFolder) : path;
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
                    var dir = Path.Combine(_repo.FullPath, selectedSingleFolder);
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
                unstage.Header = App.Text("FileCM.UnstageMulti", _selectedStaged.Count);
                unstage.Icon = App.CreateMenuIcon("Icons.File.Remove");
                unstage.Tag = "Enter/Space";
                unstage.Click += (_, e) =>
                {
                    UnstageChanges(_selectedStaged, null);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.StashMulti", _selectedStaged.Count);
                stash.Icon = App.CreateMenuIcon("Icons.Stashes.Add");
                stash.Click += (_, e) =>
                {
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new StashChanges(_repo, _selectedStaged, true));

                    e.Handled = true;
                };

                var patch = new MenuItem();
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Icon = App.CreateMenuIcon("Icons.Diff");
                patch.Click += async (_, e) =>
                {
                    var storageProvider = App.GetStorageProvider();
                    if (storageProvider == null)
                        return;

                    var options = new FilePickerSaveOptions();
                    options.Title = App.Text("FileCM.SaveAsPatch");
                    options.DefaultExtension = ".patch";
                    options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                    var storageFile = await storageProvider.SaveFilePickerAsync(options);
                    if (storageFile != null)
                    {
                        var succ = await Commands.SaveChangesAsPatch.ProcessLocalChangesAsync(_repo.FullPath, _selectedStaged, false, storageFile.Path.LocalPath);
                        if (succ)
                            App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
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
                        App.ShowWindow(new DirHistories(_repo, selectedSingleFolder));
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
                        await App.CopyTextAsync(Native.OS.GetAbsPath(_repo.FullPath, selectedSingleFolder));
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

        public ContextMenu CreateContextMenuForCommitMessages()
        {
            var menu = new ContextMenu();

            var gitTemplate = new Commands.Config(_repo.FullPath).GetAsync("commit.template").Result;
            var templateCount = _repo.Settings.CommitTemplates.Count;
            if (templateCount == 0 && string.IsNullOrEmpty(gitTemplate))
            {
                menu.Items.Add(new MenuItem()
                {
                    Header = App.Text("WorkingCopy.NoCommitTemplates"),
                    Icon = App.CreateMenuIcon("Icons.Code"),
                    IsEnabled = false
                });
            }
            else
            {
                for (int i = 0; i < templateCount; i++)
                {
                    var template = _repo.Settings.CommitTemplates[i];
                    var item = new MenuItem();
                    item.Header = App.Text("WorkingCopy.UseCommitTemplate", template.Name);
                    item.Icon = App.CreateMenuIcon("Icons.Code");
                    item.Click += (_, e) =>
                    {
                        CommitMessage = template.Apply(_repo.CurrentBranch, _staged);
                        e.Handled = true;
                    };
                    menu.Items.Add(item);
                }

                if (!string.IsNullOrEmpty(gitTemplate))
                {
                    var friendlyName = gitTemplate;
                    if (!OperatingSystem.IsWindows())
                    {
                        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        var prefixLen = home.EndsWith('/') ? home.Length - 1 : home.Length;
                        if (gitTemplate.StartsWith(home, StringComparison.Ordinal))
                            friendlyName = $"~{gitTemplate.AsSpan(prefixLen)}";
                    }

                    var gitTemplateItem = new MenuItem();
                    gitTemplateItem.Header = App.Text("WorkingCopy.UseCommitTemplate", friendlyName);
                    gitTemplateItem.Icon = App.CreateMenuIcon("Icons.Code");
                    gitTemplateItem.Click += (_, e) =>
                    {
                        if (File.Exists(gitTemplate))
                            CommitMessage = File.ReadAllText(gitTemplate);
                        e.Handled = true;
                    };
                    menu.Items.Add(gitTemplateItem);
                }
            }

            menu.Items.Add(new MenuItem() { Header = "-" });

            var historiesCount = _repo.Settings.CommitMessages.Count;
            if (historiesCount == 0)
            {
                menu.Items.Add(new MenuItem()
                {
                    Header = App.Text("WorkingCopy.NoCommitHistories"),
                    Icon = App.CreateMenuIcon("Icons.Histories"),
                    IsEnabled = false
                });
            }
            else
            {
                for (int i = 0; i < historiesCount; i++)
                {
                    var dup = _repo.Settings.CommitMessages[i].Trim();
                    var message = dup.ReplaceLineEndings(" ");
                    var item = new MenuItem();
                    item.Header = new CommitMessageRecord(message);
                    item.Icon = App.CreateMenuIcon("Icons.Histories");
                    item.Click += (_, e) =>
                    {
                        CommitMessage = dup;
                        e.Handled = true;
                    };

                    menu.Items.Add(item);
                }
            }

            return menu;
        }

        public ContextMenu CreateContextForOpenAI()
        {
            if (_staged == null || _staged.Count == 0)
            {
                App.RaiseException(_repo.FullPath, "No files added to commit!");
                return null;
            }

            var services = _repo.GetPreferredOpenAIServices();
            if (services.Count == 0)
            {
                App.RaiseException(_repo.FullPath, "Bad configuration for OpenAI");
                return null;
            }

            if (services.Count == 1)
            {
                _ = App.ShowDialog(new AIAssistant(_repo, services[0], _staged, t => CommitMessage = t));
                return null;
            }

            var menu = new ContextMenu() { Placement = PlacementMode.TopEdgeAlignedLeft };
            foreach (var service in services)
            {
                var dup = service;
                var item = new MenuItem();
                item.Header = service.Name;
                item.Click += async (_, e) =>
                {
                    await App.ShowDialog(new AIAssistant(_repo, dup, _staged, t => CommitMessage = t));
                    e.Handled = true;
                };

                menu.Items.Add(item);
            }

            return menu;
        }

        private List<Models.Change> GetVisibleChanges(List<Models.Change> changes)
        {
            if (string.IsNullOrEmpty(_filter))
                return changes;

            var visible = new List<Models.Change>();

            foreach (var c in changes)
            {
                if (c.Path.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                    visible.Add(c);
            }

            return visible;
        }

        private List<Models.Change> GetCanStagedChanges(List<Models.Change> changes)
        {
            if (!HasUnsolvedConflicts)
                return changes;

            var outs = new List<Models.Change>();
            foreach (var c in changes)
            {
                if (!c.IsConflicted)
                    outs.Add(c);
            }

            return outs;
        }

        private List<Models.Change> GetStagedChanges()
        {
            if (_useAmend)
            {
                var head = new Commands.QuerySingleCommit(_repo.FullPath, "HEAD")
                    .GetResultAsync()
                    .Result;

                return new Commands.QueryStagedChangesWithAmend(_repo.FullPath, head.Parents.Count == 0 ? Models.Commit.EmptyTreeSHA1 : $"{head.SHA}^")
                    .GetResultAsync()
                    .Result;
            }

            var rs = new List<Models.Change>();
            foreach (var c in _cached)
            {
                if (c.Index != Models.ChangeState.None)
                    rs.Add(c);
            }
            return rs;
        }

        private void UpdateDetail()
        {
            if (_selectedUnstaged.Count == 1)
                SetDetail(_selectedUnstaged[0], true);
            else if (_selectedStaged.Count == 1)
                SetDetail(_selectedStaged[0], false);
            else
                SetDetail(null, false);
        }

        private void UpdateInProgressState()
        {
            if (string.IsNullOrEmpty(_commitMessage))
            {
                var mergeMsgFile = Path.Combine(_repo.GitDir, "MERGE_MSG");
                if (File.Exists(mergeMsgFile))
                    CommitMessage = File.ReadAllText(mergeMsgFile);
            }

            if (File.Exists(Path.Combine(_repo.GitDir, "CHERRY_PICK_HEAD")))
            {
                InProgressContext = new CherryPickInProgress(_repo);
            }
            else if (Directory.Exists(Path.Combine(_repo.GitDir, "rebase-merge")) || Directory.Exists(Path.Combine(_repo.GitDir, "rebase-apply")))
            {
                var rebasing = new RebaseInProgress(_repo);
                InProgressContext = rebasing;

                if (string.IsNullOrEmpty(_commitMessage))
                {
                    var rebaseMsgFile = Path.Combine(_repo.GitDir, "rebase-merge", "message");
                    if (File.Exists(rebaseMsgFile))
                        CommitMessage = File.ReadAllText(rebaseMsgFile);
                    else if (rebasing.StoppedAt != null)
                        CommitMessage = new Commands.QueryCommitFullMessage(_repo.FullPath, rebasing.StoppedAt.SHA).GetResultAsync().Result;
                }
            }
            else if (File.Exists(Path.Combine(_repo.GitDir, "REVERT_HEAD")))
            {
                InProgressContext = new RevertInProgress(_repo);
            }
            else if (File.Exists(Path.Combine(_repo.GitDir, "MERGE_HEAD")))
            {
                InProgressContext = new MergeInProgress(_repo);
            }
            else
            {
                InProgressContext = null;
            }
        }

        private async void StageChanges(List<Models.Change> changes, Models.Change next)
        {
            var canStaged = GetCanStagedChanges(changes);
            var count = canStaged.Count;
            if (count == 0)
                return;

            IsStaging = true;
            _selectedUnstaged = next != null ? [next] : [];
            _repo.SetWatcherEnabled(false);

            var log = _repo.CreateLog("Stage");
            if (count == _unstaged.Count)
            {
                await new Commands.Add(_repo.FullPath, _repo.IncludeUntracked).Use(log).ExecAsync();
            }
            else
            {
                var pathSpecFile = Path.GetTempFileName();
                await using (var writer = new StreamWriter(pathSpecFile))
                {
                    foreach (var c in canStaged)
                        await writer.WriteLineAsync(c.Path);
                }

                await new Commands.Add(_repo.FullPath, pathSpecFile).Use(log).ExecAsync();
                File.Delete(pathSpecFile);
            }
            log.Complete();

            _repo.MarkWorkingCopyDirtyManually();
            _repo.SetWatcherEnabled(true);
            IsStaging = false;
        }

        private async void UnstageChanges(List<Models.Change> changes, Models.Change next)
        {
            var count = changes.Count;
            if (count == 0)
                return;

            IsUnstaging = true;
            _selectedStaged = next != null ? [next] : [];
            _repo.SetWatcherEnabled(false);

            var log = _repo.CreateLog("Unstage");
            if (_useAmend)
            {
                log.AppendLine("$ git update-index --index-info ");
                await new Commands.UnstageChangesForAmend(_repo.FullPath, changes).ExecAsync();
            }
            else
            {
                var pathSpecFile = Path.GetTempFileName();
                await using (var writer = new StreamWriter(pathSpecFile))
                {
                    foreach (var c in changes)
                    {
                        await writer.WriteLineAsync(c.Path);
                        if (c.Index == Models.ChangeState.Renamed)
                            await writer.WriteLineAsync(c.OriginalPath);
                    }
                }

                await new Commands.Restore(_repo.FullPath, pathSpecFile, true).Use(log).ExecAsync();
                File.Delete(pathSpecFile);
            }
            log.Complete();

            _repo.MarkWorkingCopyDirtyManually();
            _repo.SetWatcherEnabled(true);
            IsUnstaging = false;
        }

        private void SetDetail(Models.Change change, bool isUnstaged)
        {
            if (_isLoadingData)
                return;

            if (change == null)
                DetailContext = null;
            else if (change.IsConflicted)
                DetailContext = new Conflict(_repo, this, change);
            else
                DetailContext = new DiffContext(_repo.FullPath, new Models.DiffOption(change, isUnstaged), _detailContext as DiffContext);
        }

        private void DoCommit(bool autoStage, bool autoPush, CommitCheckPassed checkPassed = CommitCheckPassed.None)
        {
            if (string.IsNullOrWhiteSpace(_commitMessage))
                return;

            if (!_repo.CanCreatePopup())
            {
                App.RaiseException(_repo.FullPath, "Repository has an unfinished job! Please wait!");
                return;
            }

            if (autoStage && HasUnsolvedConflicts)
            {
                App.RaiseException(_repo.FullPath, "Repository has unsolved conflict(s). Auto-stage and commit is disabled!");
                return;
            }

            if (_repo.CurrentBranch is { IsDetachedHead: true } && checkPassed < CommitCheckPassed.DetachedHead)
            {
                var msg = App.Text("WorkingCopy.ConfirmCommitWithDetachedHead");
                _ = App.AskConfirmAsync(msg, () => DoCommit(autoStage, autoPush, CommitCheckPassed.DetachedHead));
                return;
            }

            if (!string.IsNullOrEmpty(_filter) && _staged.Count > _visibleStaged.Count && checkPassed < CommitCheckPassed.Filter)
            {
                var msg = App.Text("WorkingCopy.ConfirmCommitWithFilter", _staged.Count, _visibleStaged.Count, _staged.Count - _visibleStaged.Count);
                _ = App.AskConfirmAsync(msg, () => DoCommit(autoStage, autoPush, CommitCheckPassed.Filter));
                return;
            }

            if (checkPassed < CommitCheckPassed.FileCount && !_useAmend)
            {
                if ((!autoStage && _staged.Count == 0) || (autoStage && _cached.Count == 0))
                {
                    _ = App.ShowDialog(new ConfirmEmptyCommit(_cached.Count > 0, stageAll => DoCommit(stageAll, autoPush, CommitCheckPassed.FileCount)));
                    return;
                }
            }

            IsCommitting = true;
            _repo.Settings.PushCommitMessage(_commitMessage);
            _repo.SetWatcherEnabled(false);

            var signOff = _repo.Settings.EnableSignOffForCommit;
            var log = _repo.CreateLog("Commit");
            Task.Run(async () =>
            {
                var succ = true;
                if (autoStage && _unstaged.Count > 0)
                    succ = await new Commands.Add(_repo.FullPath, _repo.IncludeUntracked).Use(log).ExecAsync().ConfigureAwait(false);

                if (succ)
                    succ = await new Commands.Commit(_repo.FullPath, _commitMessage, signOff, _useAmend, _resetAuthor).Use(log).RunAsync().ConfigureAwait(false);

                log.Complete();

                Dispatcher.UIThread.Post(async () =>
                {
                    if (succ)
                    {
                        CommitMessage = string.Empty;
                        UseAmend = false;

                        if (autoPush && _repo.Remotes.Count > 0)
                        {
                            if (_repo.CurrentBranch == null)
                            {
                                var currentBranchName = await new Commands.QueryCurrentBranch(_repo.FullPath).GetResultAsync();
                                var tmp = new Models.Branch() { Name = currentBranchName };
                                _repo.ShowAndStartPopup(new Push(_repo, tmp));
                            }
                            else
                            {
                                _repo.ShowAndStartPopup(new Push(_repo, null));
                            }
                        }
                    }

                    _repo.MarkBranchesDirtyManually();
                    _repo.SetWatcherEnabled(true);
                    IsCommitting = false;
                });
            });
        }

        private bool IsChanged(List<Models.Change> old, List<Models.Change> cur)
        {
            if (old.Count != cur.Count)
                return true;

            for (int idx = 0; idx < old.Count; idx++)
            {
                var o = old[idx];
                var c = cur[idx];
                if (o.Path != c.Path || o.Index != c.Index || o.WorkTree != c.WorkTree)
                    return true;
            }

            return false;
        }

        private enum CommitCheckPassed
        {
            None = 0,
            DetachedHead,
            Filter,
            FileCount,
        }

        private Repository _repo;
        private bool _isLoadingData;
        private bool _isStaging;
        private bool _isUnstaging;
        private bool _isCommitting;
        private bool _useAmend;
        private bool _resetAuthor;
        private bool _hasRemotes;
        private List<Models.Change> _cached = [];
        private List<Models.Change> _unstaged = [];
        private List<Models.Change> _visibleUnstaged = [];
        private List<Models.Change> _staged = [];
        private List<Models.Change> _visibleStaged = [];
        private List<Models.Change> _selectedUnstaged = [];
        private List<Models.Change> _selectedStaged = [];
        private object _detailContext;
        private string _filter = string.Empty;
        private string _commitMessage = string.Empty;

        private bool _hasUnsolvedConflicts;
        private InProgressContext _inProgressContext;
    }
}
